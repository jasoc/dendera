using Dendera.Core.Astro;

namespace Dendera.Core.Signals;

/// <param name="Latitude">Where the person is now (photoperiod depends on it).</param>
/// <param name="Natal">If present, transit-to-natal aspects are generated.</param>
public sealed record SkyContext(double Latitude, double Longitude, NatalChart? Natal);

/// <summary>
/// Pure generator of sky signals from the ephemeris. No I/O: same input, same output.
/// </summary>
public sealed class SkySignalGenerator(IEphemeris ephemeris)
{
    public const string Source = "ephemeris";
    private const double EclipseWindowDays = 15;
    public const double RetrogradeIntensity = 1.0;
    public const double SlowMotionIntensity = 0.4;

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

    /// <summary>Signals at one instant with a precomputed eclipse list (covering ±15 days around it).</summary>
    public IReadOnlyList<Signal> At(DateTimeOffset at, SkyContext ctx, IReadOnlyList<EclipseEvent> eclipses)
    {
        var sky = ephemeris.Positions(at);
        var signals = new List<Signal>();

        Placements(at, sky, ctx, signals);
        Decans(at, sky, signals);
        Dignities(at, sky, signals);
        SolarHour(at, ctx, signals);
        FixedStars(at, sky, signals);
        Retrogrades(at, sky, signals);
        SkyAspects(at, sky, ctx, signals);
        if (ctx.Natal is not null) Transits(at, sky, ctx, signals);
        Lunar(at, signals);
        Photoperiod(at, sky, ctx, signals);
        DayPhase(at, sky, ctx, signals);
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

    /// <summary>
    /// Decans and faces: each sign in three parts of 10°, the faces ruled in Chaldean order from Mars at 0° Aries
    /// (the "Chaldean" faces of Ptolemy, Tetrabiblos I.24; face +1 in Lilly's table of dignities, CA p. 104).
    /// </summary>
    private static void Decans(DateTimeOffset at, IReadOnlyList<BodyPosition> sky, List<Signal> signals)
    {
        foreach (var p in sky.Where(p => p.Body != Bodies.NorthNode))
        {
            var index = (int)(Zodiac.Norm(p.Longitude) / 10) % 36;
            var decan = index % 3 + 1;
            var face = PlanetaryHours.Chaldean[(2 + index) % 7]; // Mars is third in the Chaldean order
            signals.Add(new Signal(
                $"decan:{p.Body}", SignalKinds.Decan, Source, at, 1.0, index + 1, "decano",
                new Dictionary<string, string> { ["body"] = p.Body, ["sign"] = p.Sign, ["decan"] = decan.ToString(), ["face"] = face },
                $"{Lexicon.Name(p.Body)} nel decano",
                $"{decan}° decano di {Lexicon.Name(p.Sign)}, faccia di {Lexicon.Name(face)}"));
        }
    }

    /// <summary>Domiciles of the seven classical planets (Ptolemy, Tetrabiblos I.17; Lilly, CA p. 104).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Domiciles = new Dictionary<string, string[]>
    {
        [Bodies.Sun] = ["leo"], [Bodies.Moon] = ["cancer"], [Bodies.Mercury] = ["gemini", "virgo"],
        [Bodies.Venus] = ["taurus", "libra"], [Bodies.Mars] = ["aries", "scorpio"],
        [Bodies.Jupiter] = ["sagittarius", "pisces"], [Bodies.Saturn] = ["capricorn", "aquarius"],
    };

    /// <summary>Exaltations (Ptolemy, Tetrabiblos I.19; Lilly, CA p. 104).</summary>
    public static readonly IReadOnlyDictionary<string, string> Exaltations = new Dictionary<string, string>
    {
        [Bodies.Sun] = "aries", [Bodies.Moon] = "taurus", [Bodies.Mercury] = "virgo", [Bodies.Venus] = "pisces",
        [Bodies.Mars] = "capricorn", [Bodies.Jupiter] = "cancer", [Bodies.Saturn] = "libra",
    };

    private static string Opposite(string sign) => Zodiac.Signs[(Zodiac.Signs.ToList().IndexOf(sign) + 6) % 12];

    /// <summary>
    /// Essential dignity by sign of the seven classical planets, with Lilly's points (CA p. 115): domicile +5,
    /// exaltation +4, detriment −5, fall −4; intensity = points / 5. Triplicities, terms and faces are not scored here.
    /// </summary>
    private static void Dignities(DateTimeOffset at, IReadOnlyList<BodyPosition> sky, List<Signal> signals)
    {
        foreach (var p in sky.Where(p => Domiciles.ContainsKey(p.Body)))
        {
            var (dignity, points) =
                Domiciles[p.Body].Contains(p.Sign) ? ("domicile", 5)
                : Exaltations[p.Body] == p.Sign ? ("exaltation", 4)
                : Domiciles[p.Body].Select(Opposite).Contains(p.Sign) ? ("detriment", -5)
                : Opposite(Exaltations[p.Body]) == p.Sign ? ("fall", -4)
                : ("none", 0);
            if (points == 0) continue;
            signals.Add(new Signal(
                $"dignity:{p.Body}", SignalKinds.Dignity, Source, at, Math.Abs(points) / 5.0, points, "punti",
                new Dictionary<string, string>
                {
                    ["body"] = p.Body, ["sign"] = p.Sign, ["dignity"] = dignity,
                    ["condition"] = points > 0 ? "dignified" : "debilitated",
                },
                $"{Lexicon.Name(p.Body)} {Lexicon.Name(dignity)}",
                $"{Lexicon.Name(p.Body)} in {Lexicon.Name(p.Sign)}: {Lexicon.Name(dignity)} ({points:+0;-0} secondo Lilly)"));
        }
    }

    /// <summary>
    /// Local mean solar hour (UTC + longitude / 15) and its four-hour watch, counted from 02:00 (02–06, 06–10…):
    /// a plain fact read by the schools that keep a clock of the day (Ayurveda, chronobiology).
    /// </summary>
    private static void SolarHour(DateTimeOffset at, SkyContext ctx, List<Signal> signals)
    {
        var u = at.UtcDateTime;
        var h = ((u.Hour + u.Minute / 60.0 + ctx.Longitude / 15.0) % 24 + 24) % 24;
        var start = (int)(((h - 2 + 24) % 24) / 4) * 4 + 2;
        var watch = $"{start % 24:00}-{(start + 4) % 24:00}";
        signals.Add(new Signal(
            "solar-hour", SignalKinds.SolarHour, Source, at, 1.0, Math.Round(h, 2), "h solari",
            new Dictionary<string, string> { ["hour"] = ((int)h).ToString(), ["watch"] = watch },
            "Ora solare locale", $"{(int)h:00}:{(int)(h % 1 * 60):00} ora solare, fascia {watch}"));
    }

    /// <summary>
    /// Fixed stars: the four royal stars and Agrippa's fifteen Behenian stars (De occulta philosophia II.47, "Of the
    /// Images of the fixed Behenian Stars"; I.32 on what is under the fixed stars). Tropical longitudes at J2000 from
    /// the standard tables, checked against 2024–2026 published positions (they differ by the expected ~0.35° of
    /// precession), precessed at 50.29″ a year. A body within 1° (convention) is conjunct.
    /// </summary>
    public static readonly IReadOnlyList<(string Id, double J2000, bool Royal)> Stars =
    [
        ("algol", 56.17, false), ("alcyone", 60.00, false), ("aldebaran", 69.79, true), ("capella", 81.85, false),
        ("sirius", 104.08, false), ("procyon", 115.78, false), ("regulus", 149.83, true), ("alkaid", 176.93, false),
        ("algorab", 193.45, false), ("spica", 203.84, false), ("arcturus", 204.23, false), ("alphecca", 222.30, false),
        ("antares", 249.77, true), ("vega", 285.32, false), ("deneb-algedi", 323.55, false), ("fomalhaut", 333.87, true),
    ];

    public const double StarOrb = 1.0;

    public static double StarLongitude(double j2000, DateTimeOffset at) =>
        Zodiac.Norm(j2000 + (at - new DateTimeOffset(2000, 1, 1, 12, 0, 0, TimeSpan.Zero)).TotalDays / 365.25 * (50.29 / 3600));

    private static void FixedStars(DateTimeOffset at, IReadOnlyList<BodyPosition> sky, List<Signal> signals)
    {
        foreach (var (id, j2000, royal) in Stars)
        {
            var star = StarLongitude(j2000, at);
            foreach (var p in sky.Where(p => p.Body != Bodies.NorthNode))
            {
                var dev = Zodiac.Separation(p.Longitude, star);
                if (dev > StarOrb) continue;
                signals.Add(new Signal(
                    $"star:{id}:{p.Body}", SignalKinds.FixedStar, Source, at, Math.Cos(dev / StarOrb * Math.PI / 2),
                    Math.Round(dev, 2), "° orb",
                    new Dictionary<string, string>
                    {
                        ["star"] = id, ["body"] = p.Body, ["sign"] = Zodiac.SignOf(star), ["royal"] = royal ? "true" : "false",
                    },
                    $"{Lexicon.Name(p.Body)} congiunto a {Lexicon.Name(id)}",
                    $"orb {dev:0.00}°, {Lexicon.Name(id)} a {Zodiac.Format(star)}"));
            }
        }
    }

    private static void Retrogrades(DateTimeOffset at, IReadOnlyList<BodyPosition> sky, List<Signal> signals)
    {
        var count = 0;
        foreach (var p in sky.Where(p => Bodies.Planets.Contains(p.Body)))
        {
            var mean = Bodies.MeanSpeed(p.Body);
            var motion = p.Speed < 0 ? "retrograde" : "direct";

            // Lilly, Christian Astrology (1647), p. 115, accidental debilities: retrograde −5, slow in motion −2.
            // Intensity keeps that ratio: retrograde 5/5, slow 2/5. Stations are their own signal below.
            if (p.Speed < 0)
            {
                count++;
                signals.Add(new Signal(
                    $"retrograde:{p.Body}", SignalKinds.Retrograde, Source, at, RetrogradeIntensity,
                    p.Speed, "°/g",
                    new Dictionary<string, string> { ["body"] = p.Body, ["sign"] = p.Sign },
                    $"{Lexicon.Name(p.Body)} retrogrado",
                    $"{Zodiac.Format(p.Longitude)}, {p.Speed:+0.000;-0.000}°/g"));
            }

            else if (p.Speed < mean)
            {
                signals.Add(new Signal(
                    $"slow:{p.Body}", SignalKinds.SlowMotion, Source, at, SlowMotionIntensity,
                    p.Speed, "°/g",
                    new Dictionary<string, string> { ["body"] = p.Body, ["sign"] = p.Sign },
                    $"{Lexicon.Name(p.Body)} lento",
                    $"{Zodiac.Format(p.Longitude)}, {p.Speed:+0.000;-0.000}°/g (medio {mean:0.000})"));
            }

            // Station: no entry in Lilly's table; convention of the instrument (speed under 10% of the mean).
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
            var hit = AspectFinder.Find(a.Longitude, a.Speed, b.Longitude, b.Speed, AspectCatalog.Default, AspectCatalog.SkyOrb(a.Body, b.Body), majorOnly: true);
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
            var hit = AspectFinder.Find(t.Longitude, t.Speed, n.Longitude, 0, AspectCatalog.Default, AspectCatalog.TransitOrb(t.Body));
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
        // Indian lunar calendar: 30 tithi of 12° of Moon–Sun elongation; the bright half (shukla paksha) is the waxing one.
        var tithi = Math.Min(30, (int)(Zodiac.Norm(phase) / 12) + 1);
        var paksha = phase < 180 ? "shukla" : "krishna";
        signals.Add(new Signal(
            "lunar-phase", SignalKinds.LunarPhase, Source, at, Math.Abs(Math.Cos(phase * Zodiac.Rad)),
            phase, "°", new Dictionary<string, string> { ["phase"] = name, ["paksha"] = paksha, ["tithi"] = tithi.ToString() },
            "Fase lunare", $"{Lexicon.Name(name)} ({phase:0}°), tithi {tithi}, {Lexicon.Name(paksha)}"));

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

    /// <summary>
    /// Day, night or twilight at the person's place, from the Sun's altitude (equatorial conversion of its ecliptic
    /// longitude, local sidereal time). Twilight = Sun within ±6° of the horizon: the sandhya of the Indian day.
    /// </summary>
    private void DayPhase(DateTimeOffset at, IReadOnlyList<BodyPosition> sky, SkyContext ctx, List<Signal> signals)
    {
        var altitude = SunAltitude(at, sky.First(p => p.Body == Bodies.Sun).Longitude, ctx.Latitude, ctx.Longitude, out var rising);
        var phase = altitude > 6 ? "day" : altitude < -6 ? "night" : rising ? "dawn" : "dusk";
        signals.Add(new Signal(
            "day-phase", SignalKinds.DayPhase, Source, at, 1.0, Math.Round(altitude, 1), "° Sole",
            new Dictionary<string, string> { ["phase"] = phase, ["light"] = altitude > 6 ? "day" : altitude < -6 ? "night" : "twilight" },
            "Giorno e notte", $"{Lexicon.Name(phase)}, Sole a {altitude:+0;-0}° sull'orizzonte"));
    }

    public double SunAltitude(DateTimeOffset at, double sunLongitude, double latitude, double longitude, out bool rising)
    {
        var eps = ephemeris.Obliquity(at) * Zodiac.Rad;
        var lambda = sunLongitude * Zodiac.Rad;
        var ra = Math.Atan2(Math.Sin(lambda) * Math.Cos(eps), Math.Cos(lambda));
        var dec = Math.Asin(Math.Sin(eps) * Math.Sin(lambda));
        var lst = (ephemeris.SiderealTimeHours(at) * 15 + longitude) * Zodiac.Rad;
        var h = lst - ra;
        var phi = latitude * Zodiac.Rad;
        rising = Math.Sin(h) < 0; // east of the meridian
        return Math.Asin(Math.Sin(phi) * Math.Sin(dec) + Math.Cos(phi) * Math.Cos(dec) * Math.Cos(h)) / Zodiac.Rad;
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
