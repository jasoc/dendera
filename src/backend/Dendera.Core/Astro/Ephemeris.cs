using CosineKitty;

namespace Dendera.Core.Astro;

/// <summary>Geocentric, tropical position of a body on the true ecliptic of date.</summary>
public sealed record BodyPosition(string Body, double Longitude, double Latitude, double Speed, double? DistanceAu)
{
    public bool Retrograde => Speed < 0 && Body != Bodies.NorthNode;
    public string Sign => Zodiac.SignOf(Longitude);
    public double DegreeInSign => Zodiac.DegreeInSign(Longitude);
}

public sealed record EclipseEvent(string Type, string Kind, DateTimeOffset Peak);

/// <summary>Pure astronomical calculations. Implementations must be deterministic and side-effect free.</summary>
public interface IEphemeris
{
    BodyPosition Position(string body, DateTimeOffset at);
    IReadOnlyList<BodyPosition> Positions(DateTimeOffset at);
    /// <summary>Greenwich apparent sidereal time, hours.</summary>
    double SiderealTimeHours(DateTimeOffset at);
    /// <summary>Obliquity of the ecliptic, degrees.</summary>
    double Obliquity(DateTimeOffset at);
    /// <summary>Moon phase angle 0..360 (0 new, 90 first quarter, 180 full, 270 last quarter).</summary>
    double MoonPhase(DateTimeOffset at);
    double MoonDistanceKm(DateTimeOffset at);
    IReadOnlyList<EclipseEvent> Eclipses(DateTimeOffset from, DateTimeOffset to);
}

/// <summary>Ephemeris backed by Astronomy Engine (MIT, ~1 arcminute accuracy, no data files).</summary>
public sealed class AstronomyEngineEphemeris : IEphemeris
{
    private const double KmPerAu = 149_597_870.7;

    public BodyPosition Position(string body, DateTimeOffset at)
    {
        if (body == Bodies.NorthNode)
        {
            return new BodyPosition(body, MeanNode(Time(at)), 0, -0.05295, null);
        }

        var (lon, lat, dist) = Ecliptic(body, Time(at));
        var (before, _, _) = Ecliptic(body, Time(at.AddHours(-12)));
        var (after, _, _) = Ecliptic(body, Time(at.AddHours(12)));
        var speed = Zodiac.Delta(before, after); // degrees over 1 day
        return new BodyPosition(body, lon, lat, speed, dist);
    }

    public IReadOnlyList<BodyPosition> Positions(DateTimeOffset at) =>
        Bodies.All.Select(b => Position(b, at)).ToList();

    public double SiderealTimeHours(DateTimeOffset at) => Astronomy.SiderealTime(Time(at));

    public double Obliquity(DateTimeOffset at)
    {
        var t = Time(at).tt / 36525.0;
        return 23.439291111 - 0.0130041667 * t - 1.64e-7 * t * t + 5.04e-7 * t * t * t;
    }

    public double MoonPhase(DateTimeOffset at) => Astronomy.MoonPhase(Time(at));

    public double MoonDistanceKm(DateTimeOffset at) => Astronomy.EclipticGeoMoon(Time(at)).dist * KmPerAu;

    public IReadOnlyList<EclipseEvent> Eclipses(DateTimeOffset from, DateTimeOffset to)
    {
        var result = new List<EclipseEvent>();
        var start = Time(from);
        var end = Time(to);

        var lunar = Astronomy.SearchLunarEclipse(start);
        while (lunar.peak.ut <= end.ut)
        {
            result.Add(new EclipseEvent("lunar", lunar.kind.ToString().ToLowerInvariant(), ToOffset(lunar.peak)));
            lunar = Astronomy.NextLunarEclipse(lunar.peak);
        }

        var solar = Astronomy.SearchGlobalSolarEclipse(start);
        while (solar.peak.ut <= end.ut)
        {
            result.Add(new EclipseEvent("solar", solar.kind.ToString().ToLowerInvariant(), ToOffset(solar.peak)));
            solar = Astronomy.NextGlobalSolarEclipse(solar.peak);
        }

        return result.OrderBy(e => e.Peak).ToList();
    }

    private static (double lon, double lat, double? distAu) Ecliptic(string body, AstroTime time)
    {
        if (body == Bodies.Moon)
        {
            var m = Astronomy.EclipticGeoMoon(time);
            return (Zodiac.Norm(m.lon), m.lat, m.dist);
        }

        var eqj = Astronomy.GeoVector(ToBody(body), time, Aberration.Corrected);
        var ect = Astronomy.RotateVector(Astronomy.Rotation_EQJ_ECT(time), eqj);
        var s = Astronomy.SphereFromVector(ect);
        return (Zodiac.Norm(s.lon), s.lat, s.dist);
    }

    /// <summary>Mean lunar ascending node (Meeus, ch. 47).</summary>
    private static double MeanNode(AstroTime time)
    {
        var t = time.tt / 36525.0;
        return Zodiac.Norm(125.0445479 - 1934.1362891 * t + 0.0020754 * t * t
                           + t * t * t / 467441.0 - t * t * t * t / 60616000.0);
    }

    private static Body ToBody(string body) => body switch
    {
        Bodies.Sun => Body.Sun,
        Bodies.Mercury => Body.Mercury,
        Bodies.Venus => Body.Venus,
        Bodies.Mars => Body.Mars,
        Bodies.Jupiter => Body.Jupiter,
        Bodies.Saturn => Body.Saturn,
        Bodies.Uranus => Body.Uranus,
        Bodies.Neptune => Body.Neptune,
        Bodies.Pluto => Body.Pluto,
        _ => throw new ArgumentException($"Unknown body '{body}'", nameof(body)),
    };

    private static AstroTime Time(DateTimeOffset at) => new(at.UtcDateTime);

    private static DateTimeOffset ToOffset(AstroTime t) =>
        new(DateTime.SpecifyKind(t.ToUtcDateTime(), DateTimeKind.Utc));
}
