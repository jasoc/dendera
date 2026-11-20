using Dendera.Api.Data;
using Dendera.Api.Services;
using Dendera.Core;
using Dendera.Core.Rules;
using Dendera.Core.Signals;
using Dendera.Core.Journal;
using Dendera.Core.Readings;

namespace Dendera.Api.Endpoints;

public sealed record ProfileInput(
    string Name,
    DateTime BirthLocal,
    int BirthUtcOffsetMinutes,
    bool BirthTimeKnown,
    string BirthPlace,
    double BirthLatitude,
    double BirthLongitude,
    string? BirthTimeZone,
    string CurrentPlace,
    double CurrentLatitude,
    double CurrentLongitude,
    string? CurrentTimeZone,
    string? HouseSystem,
    string[]? Schools)
{
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) yield return "name is required";
        if (BirthLatitude is < -90 or > 90 || CurrentLatitude is < -90 or > 90) yield return "latitude out of range";
        if (BirthLongitude is < -180 or > 180 || CurrentLongitude is < -180 or > 180) yield return "longitude out of range";
        if (BirthUtcOffsetMinutes is < -14 * 60 or > 14 * 60) yield return "utc offset out of range";
        if (BirthLocal.Year is < 1700 or > 2200) yield return "birth year out of supported range";
    }

    /// <param name="catalog">Normalises <see cref="Schools"/> (unknown ids dropped; none → all schools).</param>
    public void ApplyTo(Profile p, RuleSetCatalog catalog)
    {
        p.Name = Name.Trim();
        p.BirthLocal = DateTime.SpecifyKind(BirthLocal, DateTimeKind.Unspecified);
        p.BirthUtcOffsetMinutes = BirthUtcOffsetMinutes;
        p.BirthTimeKnown = BirthTimeKnown;
        p.BirthPlace = BirthPlace;
        p.BirthLatitude = BirthLatitude;
        p.BirthLongitude = BirthLongitude;
        p.BirthTimeZone = BirthTimeZone;
        p.CurrentPlace = CurrentPlace;
        p.CurrentLatitude = CurrentLatitude;
        p.CurrentLongitude = CurrentLongitude;
        p.CurrentTimeZone = CurrentTimeZone;
        p.HouseSystem = string.IsNullOrWhiteSpace(HouseSystem) ? "Placidus" : HouseSystem;
        p.Schools = string.Join(',', catalog.Normalize(Schools));
    }
}

public sealed record ProfileDto(
    int Id, string Name, DateTime BirthLocal, int BirthUtcOffsetMinutes, DateTimeOffset BirthUtc, bool BirthTimeKnown,
    string BirthPlace, double BirthLatitude, double BirthLongitude, string? BirthTimeZone,
    string CurrentPlace, double CurrentLatitude, double CurrentLongitude, string? CurrentTimeZone,
    string HouseSystem, IReadOnlyList<string> Schools)
{
    public static ProfileDto From(Profile p) => new(
        p.Id, p.Name, p.BirthLocal, p.BirthUtcOffsetMinutes, p.BirthUtc, p.BirthTimeKnown,
        p.BirthPlace, p.BirthLatitude, p.BirthLongitude, p.BirthTimeZone,
        p.CurrentPlace, p.CurrentLatitude, p.CurrentLongitude, p.CurrentTimeZone,
        p.HouseSystem, p.SchoolIds);
}

public sealed record ReadingResponse(Reading Reading, IReadOnlyList<SourceStatus> Sources);

public sealed record TimelineResponse(Timeline Timeline, IReadOnlyList<SourceStatus> Sources, int StepHours);

public sealed record JournalDto(
    DateOnly Date, int? Energy, int? Mood, int? Sleep, int? Focus, int? Stress, int? Gut, int? Head, int? Body,
    string? Notes)
{
    public static JournalDto From(JournalDay d) =>
        new(d.Date, d.Energy, d.Mood, d.Sleep, d.Focus, d.Stress, d.Gut, d.Head, d.Body, d.Notes);

    public IEnumerable<string> Validate()
    {
        foreach (var (name, value) in Metrics())
            if (value is < 1 or > 5) yield return $"{name} must be 1..5";
    }

    public void ApplyTo(JournalDay d)
    {
        d.Energy = Energy; d.Mood = Mood; d.Sleep = Sleep; d.Focus = Focus;
        d.Stress = Stress; d.Gut = Gut; d.Head = Head; d.Body = Body;
        d.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
    }

    public IEnumerable<(string name, int? value)> Metrics() =>
    [
        ("energy", Energy), ("mood", Mood), ("sleep", Sleep), ("focus", Focus),
        ("stress", Stress), ("gut", Gut), ("head", Head), ("body", Body),
    ];

    public JournalEntry ToCore() => new(
        Date,
        Metrics().Where(m => m.value.HasValue).ToDictionary(m => m.name, m => m.value!.Value),
        Notes);
}

public sealed record CorrelationResponse(
    DateOnly? From, DateOnly? To, int JournalDays, int TracksTested,
    IReadOnlyList<CorrelationFinding> Findings, IReadOnlyList<SourceStatus> Sources, string? Message);

public sealed record MetricInfo(string Id, string Label);

public sealed record SchoolsInput(string[] Schools);

// ---------- catalog ----------

public sealed record CatalogResponse(
    IReadOnlyList<CatalogInfluence> Influences,
    IReadOnlyList<CatalogSchool> Schools,
    IReadOnlyList<CatalogZone> Zones);

public sealed record CatalogInfluence(InfluenceInfo Info, InfluenceStatus? Status);

public sealed record CatalogSchool(
    string Id, string Version, string Name, string Description, string Tradition, string Icon,
    IReadOnlyList<string> References, IReadOnlyList<string> Requires, string Baseline, string BaselineText,
    double Saturation, double NatalAmplification, IReadOnlyList<ReferenceTable> Tables,
    IReadOnlyList<CatalogRule> Rules,
    IReadOnlyList<CatalogCorrespondence> Correspondences)
{
    public static CatalogSchool From(RuleSet s) => new(
        s.Id, s.Version, s.Name, s.Description, s.Tradition, s.Icon, s.References, s.Requires, s.Baseline,
        s.Baseline switch
        {
            BaselineStrategies.Melothesia => "Le zone dei segni e dei punti del tema natale (luminari e Ascendente pesano di più) sono più sensibili.",
            BaselineStrategies.HumanDesign => "Le zone dei centri definiti nel tema natale di Human Design sono più sensibili.",
            _ => "Nessuna sensibilità natale: conta solo ciò che accade ora.",
        },
        s.Saturation, s.NatalAmplification, s.Tables,
        s.Rules.Select(r => CatalogRule.From(s, r)).ToList(),
        s.Correspondences.Select(kv => new CatalogCorrespondence(
            kv.Key, Lexicon.Name(kv.Key), kv.Value.Select(z => new CatalogZoneRef(z, Lexicon.Name(z))).ToList())).ToList());
}

public sealed record CatalogZoneRef(string Id, string Label);

public sealed record CatalogCorrespondence(string Symbol, string Label, IReadOnlyList<CatalogZoneRef> Zones);

/// <param name="Zones">Explicit zones of the effect.</param>
/// <param name="ZonesFrom">Tags whose values are mapped to zones through the correspondences.</param>
/// <param name="ZonesFromText">Italian description of <paramref name="ZonesFrom"/>.</param>
public sealed record CatalogRule(
    string Id, string Kind, string KindLabel, IReadOnlyDictionary<string, string[]>? Tags, IReadOnlyList<string> Conditions,
    double MinIntensity,
    IReadOnlyList<CatalogZoneRef> Zones, IReadOnlyList<string> ZonesFrom, string? ZonesFromText,
    double Weight, string? Quality, string? QualityLabel, string Text)
{
    public static CatalogRule From(RuleSet s, Rule r) => new(
        r.Id, r.When.Kind, Lexicon.Kind(r.When.Kind), r.When.Tags,
        (r.When.Tags ?? []).Select(kv => $"{Lexicon.Tag(kv.Key)}: " +
            (kv.Value.Contains("*") ? "qualsiasi" : string.Join(", ", kv.Value.Select(Lexicon.Name)))).ToList(),
        r.When.MinIntensity,
        (r.Effect.Zones ?? []).Select(z => new CatalogZoneRef(z, Lexicon.Name(z))).ToList(),
        r.Effect.ZonesFrom ?? [],
        r.Effect.ZonesFrom is { Length: > 0 } from
            ? "zone corrispondenti a: " + string.Join(", ", from.Select(Lexicon.Tag))
            : null,
        r.Effect.Weight,
        r.Effect.Quality,
        r.Effect.Quality is null ? "dalla qualità dell'aspetto" : Lexicon.Name(r.Effect.Quality),
        r.Text);
}

public sealed record CatalogZone(
    string Id, string Label, string Layer, string LayerLabel, bool Systemic, string? Seat, string Description)
{
    public static CatalogZone From(BodyNode n) => new(
        n.Id, Lexicon.Name(n.Id), n.Layer, Lexicon.Name(n.Layer), n.Layer != BodyLayers.Soma, n.Seat, n.Description);
}
