namespace Dendera.Core.Astro;

public sealed record BirthData(DateTimeOffset BirthUtc, double Latitude, double Longitude);

/// <summary>A point placed in a chart: body or angle, with sign and house.</summary>
public sealed record PlacedPoint(
    string Id,
    double Longitude,
    double Latitude,
    double Speed,
    string Sign,
    double DegreeInSign,
    int House,
    bool Retrograde,
    string Label,
    string Position);

public sealed record NatalChart(
    BirthData Birth,
    HouseSystem HouseSystem,
    IReadOnlyList<double> Cusps,
    double Ascendant,
    double Midheaven,
    IReadOnlyList<PlacedPoint> Points)
{
    public PlacedPoint? Point(string id) => Points.FirstOrDefault(p => p.Id == id);

    public int HouseOf(double longitude) => Houses.HouseOf(Cusps, longitude);
}

public static class ChartCalculator
{
    public static NatalChart Compute(IEphemeris ephemeris, BirthData birth, HouseSystem system = HouseSystem.Placidus)
    {
        var at = birth.BirthUtc;
        var obliquity = ephemeris.Obliquity(at);
        var ramc = Zodiac.Norm(ephemeris.SiderealTimeHours(at) * 15.0 + birth.Longitude);
        var cusps = Houses.Cusps(system, ramc, obliquity, birth.Latitude, out var used);
        var asc = Houses.Ascendant(ramc, obliquity, birth.Latitude);
        var mc = Houses.Midheaven(ramc, obliquity);

        var points = ephemeris.Positions(at)
            .Select(p => Place(p.Body, p.Longitude, p.Latitude, p.Speed, cusps))
            .ToList();
        points.Add(Place(Bodies.Ascendant, asc, 0, 0, cusps));
        points.Add(Place(Bodies.Midheaven, mc, 0, 0, cusps));

        return new NatalChart(birth, used, cusps, asc, mc, points);
    }

    /// <summary>Places current sky positions into the houses of a (natal) chart.</summary>
    public static IReadOnlyList<PlacedPoint> PlaceSky(IEnumerable<BodyPosition> sky, IReadOnlyList<double>? cusps) =>
        sky.Select(p => Place(p.Body, p.Longitude, p.Latitude, p.Speed, cusps)).ToList();

    public static PlacedPoint Place(string id, double lon, double lat, double speed, IReadOnlyList<double>? cusps) =>
        new(
            id,
            lon,
            lat,
            speed,
            Zodiac.SignOf(lon),
            Zodiac.DegreeInSign(lon),
            cusps is null ? 0 : Houses.HouseOf(cusps, lon),
            speed < 0 && id != Bodies.NorthNode,
            Lexicon.Name(id),
            Zodiac.Format(lon));
}
