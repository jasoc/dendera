using Dendera.Api.Data;
using Dendera.Core.Astro;
using Dendera.Core.Rules;
using Dendera.Core.Signals;

namespace Dendera.Api.Services;

/// <summary>Everything derived from a stored profile that the pipeline needs.</summary>
public sealed record ProfileContext(Profile Profile, NatalChart Natal, IReadOnlyList<RuleSet> Schools, NatalBaselines Baselines)
{
    public TimeSpan CurrentOffset(DateTimeOffset at) => TimeZones.OffsetAt(Profile.CurrentTimeZone, at);
}

public sealed class ProfileContextFactory(IEphemeris ephemeris, RuleSetCatalog catalog)
{
    /// <param name="schools">Override of the profile's active schools (not saved); null → the profile's own.</param>
    public ProfileContext Create(Profile profile, IEnumerable<string>? schools = null)
    {
        var system = Enum.TryParse<HouseSystem>(profile.HouseSystem, true, out var hs) ? hs : HouseSystem.Placidus;
        var natal = ChartCalculator.Compute(ephemeris,
            new BirthData(profile.BirthUtc, profile.BirthLatitude, profile.BirthLongitude), system);
        var active = catalog.Resolve(schools ?? profile.SchoolIds);
        var baselines = NatalBaselines.For(active, natal,
            () => HumanDesign.Compute(ephemeris, natal.Birth.BirthUtc).DefinedCenters);
        return new ProfileContext(profile, natal, active, baselines);
    }

    /// <summary>Parses "?schools=a,b" (null or blank → no override).</summary>
    public static IReadOnlyList<string>? ParseOverride(string? schools) =>
        string.IsNullOrWhiteSpace(schools)
            ? null
            : schools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public static class TimeZones
{
    public static TimeZoneInfo? Find(string? ianaId)
    {
        if (string.IsNullOrWhiteSpace(ianaId)) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(ianaId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
    }

    public static TimeSpan OffsetAt(string? ianaId, DateTimeOffset at) =>
        Find(ianaId)?.GetUtcOffset(at) ?? TimeSpan.Zero;

    /// <summary>Offset in effect for a local wall-clock time (best effort; historical DST may be imprecise).</summary>
    public static int? OffsetMinutesForLocal(string? ianaId, DateTime local)
    {
        var tz = Find(ianaId);
        return tz is null ? null : (int)tz.GetUtcOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified)).TotalMinutes;
    }
}
