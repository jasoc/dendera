using Dendera.Api.Data;
using Dendera.Core.Astro;
using Dendera.Core.Rules;
using Dendera.Core.Signals;

namespace Dendera.Api.Services;

/// <summary>Everything derived from a stored profile that the pipeline needs.</summary>
public sealed record ProfileContext(Profile Profile, NatalChart Natal, RuleSet Rules, SkyContext Sky)
{
    public TimeSpan CurrentOffset(DateTimeOffset at) => TimeZones.OffsetAt(Profile.CurrentTimeZone, at);
}

public sealed class ProfileContextFactory(IEphemeris ephemeris, RuleSetCatalog catalog)
{
    public ProfileContext Create(Profile profile, string? ruleSetId = null)
    {
        var system = Enum.TryParse<HouseSystem>(profile.HouseSystem, true, out var hs) ? hs : HouseSystem.Placidus;
        var natal = ChartCalculator.Compute(ephemeris,
            new BirthData(profile.BirthUtc, profile.BirthLatitude, profile.BirthLongitude), system);
        var rules = catalog.Get(ruleSetId ?? profile.RuleSetId);
        var sky = new SkyContext(profile.CurrentLatitude, profile.CurrentLongitude, natal, rules.Aspects,
            rules.Orbs.Transit, rules.Orbs.Sky);
        return new ProfileContext(profile, natal, rules, sky);
    }
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
