using Dendera.Core.Astro;

namespace Dendera.Core.Signals;

/// <param name="Latitude">Where the person is now (photoperiod depends on it).</param>
/// <param name="Natal">If present, transit-to-natal aspects are generated.</param>
/// <param name="Aspects">Aspect definitions (from the rule set).</param>
/// <param name="TransitOrbFactor">Multiplier applied to aspect orbs for transits to natal points.</param>
/// <param name="SkyOrbFactor">Multiplier applied to aspect orbs for planet-to-planet aspects in the sky.</param>
public sealed record SkyContext(
    double Latitude,
    double Longitude,
    NatalChart? Natal,
    IReadOnlyList<AspectSpec> Aspects,
    double TransitOrbFactor = 0.4,
    double SkyOrbFactor = 0.5);

/// <summary>
/// Pure generator of sky signals from the ephemeris. No I/O: same input, same output.
/// </summary>
public sealed class SkySignalGenerator(IEphemeris ephemeris)
{
    public const string Source = "ephemeris";
    private const double EclipseWindowDays = 15;

    public IReadOnlyList<Signal> At(DateTimeOffset at, SkyContext ctx) =>
        At(at, ctx, ephemeris.Eclipses(at.AddDays(-EclipseWindowDays), at.AddDays(EclipseWindowDays)));

    /// <summary>Samples the sky every <paramref name="step"/> between from and to (inclusive).</summary>
    public IReadOnlyList<Signal> Range(DateTimeOffset from, DateTimeOffset to, TimeSpan step, SkyContext ctx)
    {
        var eclipses = ephemeris.Eclipses(from.AddDays(-EclipseWindowDays), to.AddDays(EclipseWindowDays));
        var result = new List<Signal>();
        for (var t = from; t <= to; t += step) result.AddRange(At(t, ctx, eclipses));
        return result;
    }

    private IReadOnlyList<Signal> At(DateTimeOffset at, SkyContext ctx, IReadOnlyList<EclipseEvent> eclipses)
    {
        var sky = ephemeris.Positions(at);
        var signals = new List<Signal>();

        Placements(at, sky, ctx, signals);
        Retrogrades(at, sky, signals);
        SkyAspects(at, sky, ctx, signals);
        if (ctx.Natal is not null) Transits(at, sky, ctx, signals);
        Lunar(at, signals);
        Photoperiod(at, sky, ctx, signals);
        Eclipses(at, eclipses, signals);

        return signals;
    }

    private static void Placements(DateTimeOffset at, IReadOnlyList<BodyPosition> sky, SkyContext ctx, List<Signal> signals)
    {
        foreach (var p in sky)
        {
            var tags = new Dictionary<string, string> { ["body"] = p.Body, ["sign"] = p.Sign };
            if (ctx.Natal is not null) tags["house"] = ctx.Natal.HouseOf(p.Longitude).ToString();
            signals.Add(new Signal(
                $"placement:{p.Body}", SignalKinds.Placement, Source, at, 1.0, p.Longitude, "°", tags,
                $"{Lexicon.Name(p.Body)} in {Lexicon.Name(p.Sign)}",
                Zodiac.Format(p.Longitude)));
        }
    }

    private static void Retrogrades(DateTimeOffset at, IReadOnlyList<BodyPosition> sky, List<Signal> signals)
    {
        var count = 0;
        foreach (var p in sky.Where(p => Bodies.Planets.Contains(p.Body)))
        {
            var mean = Bodies.MeanSpeed(p.Body);
            var motion = p.Speed < 0 ? "retrograde" : "direct";

            if (p.Speed < 0)
            {
                count++;
                // Stations are traditionally the most potent moments: strength grows as speed approaches 0.
                var stationFactor = 1 - Math.Min(1, Math.Abs(p.Speed) / (0.25 * mean));
                signals.Add(new Signal(
                    $"retrograde:{p.Body}", SignalKinds.Retrograde, Source, at, 0.5 + 0.5 * stationFactor,
                    p.Speed, "°/g",
                    new Dictionary<string, string> { ["body"] = p.Body, ["sign"] = p.Sign },
                    $"{Lexicon.Name(p.Body)} retrogrado",
                    $"{Zodiac.Format(p.Longitude)}, {p.Speed:+0.000;-0.000}°/g"));
            }

            if (Math.Abs(p.Speed) < 0.1 * mean)
            {
                signals.Add(new Signal(
                    $"station:{p.Body}", SignalKinds.Station, Source, at, 1 - Math.Abs(p.Speed) / (0.1 * mean),
                    p.Speed, "°/g",
                    new Dictionary<string, string> { ["body"] = p.Body, ["sign"] = p.Sign, ["motion"] = motion },
                    $"{Lexicon.Name(p.Body)} stazionario",
                    $"{Zodiac.Format(p.Longitude)}, quasi fermo ({p.Speed:+0.0000;-0.0000}°/g)"));
            }
        }

        if (count > 0)
        {
            signals.Add(new Signal(
                "retrograde-count", SignalKinds.RetrogradeCount, Source, at, count / (double)Bodies.Planets.Count,
                count, "pianeti", new Dictionary<string, string> { ["count"] = count.ToString() },
                "Pianeti retrogradi", $"{count} su {Bodies.Planets.Count}"));
        }
    }

    private static void SkyAspects(DateTimeOffset at, IReadOnlyList<BodyPosition> sky, SkyContext ctx, List<Signal> signals)
    {
        var bodies = sky.Where(p => p.Body != Bodies.NorthNode).ToList();
        for (var i = 0; i < bodies.Count; i++)
        for (var j = i + 1; j < bodies.Count; j++)
        {
            var a = bodies[i];
            var b = bodies[j];
            var hit = AspectFinder.Find(a.Longitude, a.Speed, b.Longitude, b.Speed, ctx.Aspects, ctx.SkyOrbFactor, majorOnly: true);
            if (hit is null) continue;

            signals.Add(new Signal(
                $"sky:{a.Body}:{hit.Aspect.Id}:{b.Body}", SignalKinds.SkyAspect, Source, at, hit.Strength,
                hit.Deviation, "° orb",
                new Dictionary<string, string>
                {
                    ["bodyA"] = a.Body, ["bodyB"] = b.Body, ["aspect"] = hit.Aspect.Id,
                    ["quality"] = hit.Aspect.Quality, ["signA"] = a.Sign, ["signB"] = b.Sign,
                    ["applying"] = hit.Applying ? "true" : "false",
                },
                $"{Lexicon.Name(a.Body)} {Lexicon.Name(hit.Aspect.Id)} {Lexicon.Name(b.Body)}",
                $"orb {hit.Deviation:0.00}°, {(hit.Applying ? "applicativo" : "separativo")}"));
        }
    }

    private static void Transits(DateTimeOffset at, IReadOnlyList<BodyPosition> sky, SkyContext ctx, List<Signal> signals)
    {
        var natal = ctx.Natal!;
        foreach (var t in sky.Where(p => p.Body != Bodies.NorthNode))
        foreach (var n in natal.Points)
        {
            var hit = AspectFinder.Find(t.Longitude, t.Speed, n.Longitude, 0, ctx.Aspects, ctx.TransitOrbFactor);
            if (hit is null) continue;

            signals.Add(new Signal(
                $"transit:{t.Body}:{hit.Aspect.Id}:{n.Id}", SignalKinds.TransitAspect, Source, at, hit.Strength,
                hit.Deviation, "° orb",
                new Dictionary<string, string>
                {
                    ["transitBody"] = t.Body, ["natalPoint"] = n.Id, ["aspect"] = hit.Aspect.Id,
                    ["quality"] = hit.Aspect.Quality, ["transitSign"] = t.Sign, ["natalSign"] = n.Sign,
                    ["natalHouse"] = n.House.ToString(), ["applying"] = hit.Applying ? "true" : "false",
                },
                $"{Lexicon.Name(t.Body)} {Lexicon.Name(hit.Aspect.Id)} {Lexicon.Name(n.Id)} natale",
                $"orb {hit.Deviation:0.00}°, {(hit.Applying ? "applicativo" : "separativo")}, casa {n.House}"));
        }
    }

    private void Lunar(DateTimeOffset at, List<Signal> signals)
    {
        var phase = ephemeris.MoonPhase(at);
        var name = PhaseName(phase);
        signals.Add(new Signal(
            "lunar-phase", SignalKinds.LunarPhase, Source, at, Math.Abs(Math.Cos(phase * Zodiac.Rad)),
            phase, "°", new Dictionary<string, string> { ["phase"] = name },
            "Fase lunare", $"{Lexicon.Name(name)} ({phase:0}°)"));

        const double perigee = 356_400, apogee = 406_700;
        var km = ephemeris.MoonDistanceKm(at);
        var closeness = Math.Clamp((apogee - km) / (apogee - perigee), 0, 1);
        signals.Add(new Signal(
            "lunar-distance", SignalKinds.LunarDistance, Source, at, closeness, Math.Round(km), "km",
            new Dictionary<string, string> { ["zone"] = closeness > 0.85 ? "perigee" : closeness < 0.15 ? "apogee" : "mid" },
            "Vicinanza della Luna", $"{km:N0} km"));
    }

    private static void Photoperiod(DateTimeOffset at, IReadOnlyList<BodyPosition> sky, SkyContext ctx, List<Signal> signals)
    {
        var sun = sky.First(p => p.Body == Bodies.Sun);
        var hours = DayLength(sun.Longitude, ctx.Latitude);
        var next = DayLength(sun.Longitude + sun.Speed, ctx.Latitude);
        var deltaMin = (next - hours) * 60;

        signals.Add(new Signal(
            "photoperiod", SignalKinds.Photoperiod, Source, at, Math.Clamp((12.5 - hours) / 4.5, 0, 1),
            Math.Round(hours, 2), "ore luce", new Dictionary<string, string>(),
            "Carenza di luce", $"{(int)hours}h {(int)((hours % 1) * 60):00}m di luce"));

        signals.Add(new Signal(
            "photoperiod-change", SignalKinds.PhotoperiodChange, Source, at, Math.Clamp(Math.Abs(deltaMin) / 4, 0, 1),
            Math.Round(deltaMin, 2), "min/g",
            new Dictionary<string, string> { ["direction"] = deltaMin < 0 ? "shrinking" : "growing" },
            "Variazione della luce", $"{deltaMin:+0.0;-0.0} min/giorno"));
    }

    private static void Eclipses(DateTimeOffset at, IReadOnlyList<EclipseEvent> eclipses, List<Signal> signals)
    {
        foreach (var e in eclipses)
        {
            var days = (at - e.Peak).TotalDays;
            if (Math.Abs(days) > EclipseWindowDays) continue;
            signals.Add(new Signal(
                $"eclipse:{e.Type}", SignalKinds.Eclipse, Source, at, 1 - Math.Abs(days) / EclipseWindowDays,
                Math.Round(days, 1), "giorni", new Dictionary<string, string> { ["type"] = e.Type, ["kind"] = e.Kind },
                e.Type == "solar" ? "Eclissi di Sole" : "Eclissi di Luna",
                $"{e.Kind}, picco {e.Peak:yyyy-MM-dd HH:mm} UTC ({days:+0.0;-0.0} g)"));
        }
    }

    /// <summary>Hours of daylight (sunrise to sunset, standard refraction) for a solar longitude and latitude.</summary>
    public static double DayLength(double sunLongitude, double latitude)
    {
        const double obliquity = 23.44;
        var decl = Math.Asin(Math.Sin(obliquity * Zodiac.Rad) * Math.Sin(sunLongitude * Zodiac.Rad));
        var phi = latitude * Zodiac.Rad;
        var cosH = (Math.Sin(-0.833 * Zodiac.Rad) - Math.Sin(phi) * Math.Sin(decl)) / (Math.Cos(phi) * Math.Cos(decl));
        if (cosH <= -1) return 24;
        if (cosH >= 1) return 0;
        return 2 * Math.Acos(cosH) / Zodiac.Rad / 15.0;
    }

    public static string PhaseName(double phase) => (int)(Zodiac.Norm(phase + 22.5) / 45) switch
    {
        0 => "new",
        1 => "waxing-crescent",
        2 => "first-quarter",
        3 => "waxing-gibbous",
        4 => "full",
        5 => "waning-gibbous",
        6 => "last-quarter",
        _ => "waning-crescent",
    };
}
