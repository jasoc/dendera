using Dendera.Core.Rules;

namespace Dendera.Core.Calibration;

/// <summary>A reference place standing for its climate (Köppen–Geiger class).</summary>
public sealed record ClimateZone(string Id, string Koppen, string Name, double Latitude, double Longitude);

/// <summary>
/// Climate zones of the reference set. Schools that read the local weather are calibrated per zone, so a summer day
/// in Palermo is compared with Palermo's climate and one in Warsaw with Warsaw's. A person's place is assigned to the
/// nearest reference place: an approximation (no climate map is bundled), written in the calibration report.
/// </summary>
public static class ClimateZones
{
    /// <summary>Influence id of the local weather: schools requiring it are calibrated per climate zone.</summary>
    public const string WeatherInfluence = "open-meteo";

    public static readonly IReadOnlyList<ClimateZone> All =
    [
        new("csa-palermo", "Csa", "Mediterraneo (Palermo)", 38.12, 13.36),
        new("cfa-milano", "Cfa", "Subtropicale umido (Milano)", 45.46, 9.19),
        new("cfb-london", "Cfb", "Oceanico (Londra)", 51.51, -0.13),
        new("dfb-warszawa", "Dfb", "Continentale (Varsavia)", 52.23, 21.01),
        new("bsk-madrid", "BSk", "Semiarido (Madrid)", 40.42, -3.70),
        new("bwh-cairo", "BWh", "Desertico caldo (Il Cairo)", 30.04, 31.24),
        new("af-singapore", "Af", "Equatoriale (Singapore)", 1.35, 103.82),
    ];

    public static bool IsClimateDependent(RuleSet school) => school.Requires.Contains(WeatherInfluence);

    /// <summary>The reference place nearest to a location (great-circle distance).</summary>
    public static ClimateZone Nearest(double latitude, double longitude) =>
        All.MinBy(z => Distance(latitude, longitude, z.Latitude, z.Longitude))!;

    /// <summary>Table id for a school at a place: a climate zone id, or <see cref="SchoolCalibration.Global"/>.</summary>
    public static string TableFor(RuleSet school, double latitude, double longitude) =>
        IsClimateDependent(school) ? Nearest(latitude, longitude).Id : SchoolCalibration.Global;

    private static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        const double rad = Math.PI / 180;
        var a = Math.Pow(Math.Sin((lat2 - lat1) * rad / 2), 2) +
                Math.Cos(lat1 * rad) * Math.Cos(lat2 * rad) * Math.Pow(Math.Sin((lon2 - lon1) * rad / 2), 2);
        return 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}
