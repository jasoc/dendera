using System.Reflection;
using Dendera.Api.Data;
using Dendera.Api.Services;
using Dendera.Core;
using Dendera.Core.Astro;
using Dendera.Core.Journal;
using Dendera.Core.Readings;
using Dendera.Core.Rules;
using Dendera.Core.Signals;
using Dendera.Sources;
using Microsoft.EntityFrameworkCore;

namespace Dendera.Api.Endpoints;

public static class ApiEndpoints
{
    private const int MaxTimelineDays = 180;

    public static void MapDenderaApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        // The image's version (InformationalVersion, set by the Dockerfile's DENDERA_VERSION
        // build arg), so a running container always says what it is.
        var version = typeof(ApiEndpoints).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        api.MapGet("/health", () => Results.Ok(new { status = "ok", version }));

        // ---------- reference data ----------
        api.MapGet("/geocode", async (string q, OpenMeteoGeocoder geo, CancellationToken ct) =>
            Results.Ok(await geo.SearchAsync(q, "it", ct)));

        api.MapGet("/timezone/offset", (string tz, DateTime local) =>
            TimeZones.OffsetMinutesForLocal(tz, local) is { } minutes
                ? Results.Ok(new { offsetMinutes = minutes })
                : Results.NotFound(new { error = $"unknown time zone '{tz}'" }));

        // What the code implements: every influence (with its last status), every school, the body vocabulary.
        api.MapGet("/catalog", (RuleSetCatalog catalog, IEnumerable<ISignalProvider> providers, InfluenceStatusBoard board) =>
            Results.Ok(new CatalogResponse(
                providers.Select(p => new CatalogInfluence(p.Info, board.Last(p.Source))).ToList(),
                catalog.All.Select(CatalogSchool.From).ToList(),
                BodyZones.Nodes.Select(CatalogZone.From).ToList())));

        api.MapGet("/metrics", () =>
            Results.Ok(JournalMetrics.All.Select(m => new MetricInfo(m, Lexicon.Name(m)))));

        api.MapGet("/sky", (DateTimeOffset? at, IEphemeris eph, TimeProvider clock) =>
        {
            var when = at ?? clock.GetUtcNow();
            return Results.Ok(ChartCalculator.PlaceSky(eph.Positions(when), null));
        });

        // ---------- profiles ----------
        var profiles = api.MapGroup("/profiles");

        profiles.MapGet("/", async (DenderaDb db) =>
            Results.Ok((await db.Profiles.OrderBy(p => p.Id).ToListAsync()).Select(ProfileDto.From)));

        profiles.MapGet("/{id:int}", async (int id, DenderaDb db) =>
            await db.Profiles.FindAsync(id) is { } p ? Results.Ok(ProfileDto.From(p)) : Results.NotFound());

        profiles.MapPost("/", async (ProfileInput input, DenderaDb db, RuleSetCatalog catalog, TimeProvider clock) =>
        {
            var errors = input.Validate().ToList();
            if (errors.Count > 0) return Results.BadRequest(new { errors });
            var p = new Profile { CreatedAt = clock.GetUtcNow() };
            input.ApplyTo(p, catalog);
            db.Profiles.Add(p);
            await db.SaveChangesAsync();
            return Results.Created($"/api/profiles/{p.Id}", ProfileDto.From(p));
        });

        profiles.MapPut("/{id:int}", async (int id, ProfileInput input, DenderaDb db, RuleSetCatalog catalog) =>
        {
            var errors = input.Validate().ToList();
            if (errors.Count > 0) return Results.BadRequest(new { errors });
            if (await db.Profiles.FindAsync(id) is not { } p) return Results.NotFound();
            input.ApplyTo(p, catalog);
            await db.SaveChangesAsync();
            return Results.Ok(ProfileDto.From(p));
        });

        // The fast toggle: only the active schools.
        profiles.MapPut("/{id:int}/schools", async (int id, SchoolsInput input, DenderaDb db, RuleSetCatalog catalog) =>
        {
            if (await db.Profiles.FindAsync(id) is not { } p) return Results.NotFound();
            p.Schools = string.Join(',', catalog.Normalize(input.Schools));
            await db.SaveChangesAsync();
            return Results.Ok(ProfileDto.From(p));
        });

        profiles.MapDelete("/{id:int}", async (int id, DenderaDb db) =>
        {
            if (await db.Profiles.FindAsync(id) is not { } p) return Results.NotFound();
            db.Profiles.Remove(p);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        profiles.MapGet("/{id:int}/natal", async (int id, DenderaDb db, ProfileContextFactory factory) =>
            await db.Profiles.FindAsync(id) is { } p ? Results.Ok(factory.Create(p).Natal) : Results.NotFound());

        // ---------- the pipeline ----------
        profiles.MapGet("/{id:int}/reading", async (
            int id, DateTimeOffset? at, string? schools, string? consensus,
            DenderaDb db, ProfileContextFactory factory, SignalHub hub, IEphemeris eph, TimeProvider clock,
            CancellationToken ct) =>
        {
            if (await db.Profiles.FindAsync([id], ct) is not { } p) return Results.NotFound();
            var ctx = factory.Create(p, ProfileContextFactory.ParseOverride(schools));
            var when = at ?? clock.GetUtcNow();

            var bundle = await hub.CollectAsync([when], p.CurrentLatitude, p.CurrentLongitude, ctx.Natal, ct,
                await IllnessOnsets(db, ctx, ct));
            var active = bundle.ActiveAt()(when);
            var reading = ReadingEngine.Compose(when, ctx.Schools, ctx.Baselines, eph.Positions(when), ctx.Natal, active,
                ParseConsensus(consensus));
            return Results.Ok(new ReadingResponse(reading, bundle.Sources));
        });

        profiles.MapGet("/{id:int}/timeline", async (
            int id, DateTimeOffset? from, DateTimeOffset? to, int? stepHours, string? schools, string? consensus,
            DenderaDb db, ProfileContextFactory factory, SignalHub hub, TimeProvider clock, CancellationToken ct) =>
        {
            if (await db.Profiles.FindAsync([id], ct) is not { } p) return Results.NotFound();
            var now = clock.GetUtcNow();
            var end = to ?? now.AddDays(7);
            var start = from ?? end.AddDays(-37);
            if (end <= start) return Results.BadRequest(new { error = "'to' must be after 'from'" });
            if ((end - start).TotalDays > MaxTimelineDays)
                return Results.BadRequest(new { error = $"max {MaxTimelineDays} days per request" });

            var step = Math.Clamp(stepHours ?? 6, 1, 24);
            var ctx = factory.Create(p, ProfileContextFactory.ParseOverride(schools));
            var (timeline, sources) = await BuildTimeline(ctx, Align(start, step), end, step, hub, db, ct, ParseConsensus(consensus));
            return Results.Ok(new TimelineResponse(timeline, sources, step));
        });

        // ---------- journal ----------
        profiles.MapGet("/{id:int}/journal", async (int id, DateOnly? from, DateOnly? to, DenderaDb db) =>
        {
            var q = db.Journal.Where(j => j.ProfileId == id);
            if (from is { } f) q = q.Where(j => j.Date >= f);
            if (to is { } t) q = q.Where(j => j.Date <= t);
            return Results.Ok((await q.OrderByDescending(j => j.Date).ToListAsync()).Select(JournalDto.From));
        });

        profiles.MapPut("/{id:int}/journal/{date}", async (int id, DateOnly date, JournalDto input, DenderaDb db, TimeProvider clock) =>
        {
            var errors = input.Validate().ToList();
            if (errors.Count > 0) return Results.BadRequest(new { errors });
            if (await db.Profiles.FindAsync(id) is null) return Results.NotFound();

            var day = await db.Journal.SingleOrDefaultAsync(j => j.ProfileId == id && j.Date == date);
            if (day is null)
            {
                day = new JournalDay { ProfileId = id, Date = date };
                db.Journal.Add(day);
            }
            input.ApplyTo(day);
            day.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            return Results.Ok(JournalDto.From(day));
        });

        profiles.MapDelete("/{id:int}/journal/{date}", async (int id, DateOnly date, DenderaDb db) =>
        {
            var deleted = await db.Journal.Where(j => j.ProfileId == id && j.Date == date).ExecuteDeleteAsync();
            return deleted == 0 ? Results.NotFound() : Results.NoContent();
        });

        // ---------- correlations: journal metrics against signal and zone tracks ----------
        profiles.MapGet("/{id:int}/correlations", async (
            int id, string? schools, string? consensus, DenderaDb db, ProfileContextFactory factory, SignalHub hub, CancellationToken ct) =>
        {
            if (await db.Profiles.FindAsync([id], ct) is not { } p) return Results.NotFound();
            var days = await db.Journal.Where(j => j.ProfileId == id).OrderBy(j => j.Date).ToListAsync(ct);
            var options = new CorrelationOptions();
            if (days.Count < options.MinDays)
            {
                return Results.Ok(new CorrelationResponse(null, null, days.Count, 0, [], [],
                    $"Servono almeno {options.MinDays} giorni di diario (ora: {days.Count})."));
            }

            var first = days[0].Date;
            var last = days[^1].Date;
            if (last.DayNumber - first.DayNumber > MaxTimelineDays) first = last.AddDays(-MaxTimelineDays);

            var ctx = factory.Create(p, ProfileContextFactory.ParseOverride(schools));
            var offset = ctx.CurrentOffset(DateTimeOffset.UtcNow);
            var start = new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), offset);
            var end = new DateTimeOffset(last.ToDateTime(new TimeOnly(23, 0)), offset);
            var (timeline, sources) = await BuildTimeline(ctx, start, end, 6, hub, db, ct, ParseConsensus(consensus));

            var series = CorrelationEngine.Daily(timeline.Signals.Concat(timeline.Zones), offset);
            var journal = days.Where(d => d.Date >= first).Select(d => JournalDto.From(d).ToCore()).ToList();
            var findings = CorrelationEngine.Analyze(journal, series, options);
            return Results.Ok(new CorrelationResponse(first, last, journal.Count, series.Count, findings, sources, null));
        });
    }

    private static async Task<(Timeline, IReadOnlyList<SourceStatus>)> BuildTimeline(
        ProfileContext ctx, DateTimeOffset start, DateTimeOffset end, int stepHours, SignalHub hub, DenderaDb db, CancellationToken ct,
        ConsensusMode consensus = ConsensusMode.Plain)
    {
        var frames = new List<DateTimeOffset>();
        for (var t = start; t <= end; t = t.AddHours(stepHours)) frames.Add(t);

        var p = ctx.Profile;
        var bundle = await hub.CollectAsync(frames, p.CurrentLatitude, p.CurrentLongitude, ctx.Natal, ct,
            await IllnessOnsets(db, ctx, ct));
        var timeline = ReadingEngine.BuildTimeline(start, end, ctx.Schools, ctx.Baselines,
            bundle.All, frames, bundle.ActiveAt(), consensus);
        return (timeline, bundle.Sources);
    }

    /// <summary>"?consensus=lineage" → one vote per family of traditions; anything else → one vote per school.</summary>
    private static ConsensusMode ParseConsensus(string? consensus) =>
        string.Equals(consensus, "lineage", StringComparison.OrdinalIgnoreCase) ? ConsensusMode.Lineage : ConsensusMode.Plain;

    /// <summary>Onsets of malaise recorded in the journal (local time of the current place → UTC), for the decumbiture.</summary>
    private static async Task<IReadOnlyList<DateTimeOffset>> IllnessOnsets(DenderaDb db, ProfileContext ctx, CancellationToken ct)
    {
        var id = ctx.Profile.Id;
        var days = await db.Journal.Where(j => j.ProfileId == id && j.IllnessOnset != null)
            .Select(j => new { j.Date, j.IllnessOnset }).ToListAsync(ct);
        return days.Select(d =>
        {
            var local = d.Date.ToDateTime(d.IllnessOnset!.Value);
            return new DateTimeOffset(local, ctx.CurrentOffset(new DateTimeOffset(local, TimeSpan.Zero))).ToUniversalTime();
        }).OrderBy(t => t).ToList();
    }

    /// <summary>Align to whole step hours (UTC) so cached external samples line up with frames.</summary>
    private static DateTimeOffset Align(DateTimeOffset t, int stepHours)
    {
        var u = t.ToUniversalTime();
        return new DateTimeOffset(u.Year, u.Month, u.Day, u.Hour - u.Hour % stepHours, 0, 0, TimeSpan.Zero);
    }
}
