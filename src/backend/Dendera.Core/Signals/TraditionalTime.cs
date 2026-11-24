using Dendera.Core.Astro;

namespace Dendera.Core.Signals;

/// <summary>
/// Planetary hours: 12 unequal hours from sunrise to sunset and 12 from sunset to sunrise, ruled in Chaldean order;
/// the day belongs to the planet of its first hour (Vettius Valens, Anthology I; Picatrix II; Lilly, Christian
/// Astrology I, table of planetary hours). Sunrise and sunset at −0.833° (refraction and the Sun's radius).
/// </summary>
public static class PlanetaryHours
{
    /// <summary>Chaldean order, slowest to fastest.</summary>
    public static readonly IReadOnlyList<string> Chaldean =
        [Bodies.Saturn, Bodies.Jupiter, Bodies.Mars, Bodies.Sun, Bodies.Venus, Bodies.Mercury, Bodies.Moon];

    /// <summary>Ruler of each weekday, Sunday first (DayOfWeek order).</summary>
    public static readonly IReadOnlyList<string> DayRulers =
        [Bodies.Sun, Bodies.Moon, Bodies.Mars, Bodies.Mercury, Bodies.Jupiter, Bodies.Venus, Bodies.Saturn];

    public sealed record Hour(string Ruler, string DayRuler, int Number, bool Day, DateTimeOffset Start, DateTimeOffset End);

    private const double Horizon = -0.833;

    /// <summary>The planetary hour at an instant and place; null where the Sun does not rise or set (polar day/night).</summary>
    public static Hour? At(IEphemeris ephemeris, DateTimeOffset at, double latitude, double longitude)
    {
        var sky = new SkySignalGenerator(ephemeris);
        double Alt(DateTimeOffset t) =>
            sky.SunAltitude(t, ephemeris.Position(Bodies.Sun, t).Longitude, latitude, longitude, out _) - Horizon;

        var prev = Crossing(Alt, at, -1);
        var next = Crossing(Alt, at, +1);
        if (prev is null || next is null) return null;

        var (start, rising) = prev.Value;
        var end = next.Value.At;
        var index = Math.Clamp((int)((at - start) / (end - start) * 12), 0, 11);
        // The planetary day starts at sunrise: by night, it is the day of the sunrise before the sunset.
        var sunriseLocal = (rising ? start : start.AddHours(-12)).ToUniversalTime().AddHours(longitude / 15).DateTime;
        var dayRuler = DayRulers[(int)sunriseLocal.DayOfWeek];
        var n = (rising ? 0 : 12) + index;
        var ruler = Chaldean[(Chaldean.ToList().IndexOf(dayRuler) + n) % 7];
        var length = (end - start) / 12;
        return new Hour(ruler, dayRuler, n + 1, rising, start + length * index, start + length * (index + 1));
    }

    /// <summary>The nearest horizon crossing before (dir −1) or after (dir +1) an instant, within 28 hours.</summary>
    private static (DateTimeOffset At, bool Rising)? Crossing(Func<DateTimeOffset, double> alt, DateTimeOffset at, int dir)
    {
        var step = TimeSpan.FromMinutes(20 * dir);
        var t0 = at;
        var a0 = alt(t0);
        for (var i = 0; i < 84; i++)
        {
            var t1 = t0 + step;
            var a1 = alt(t1);
            if (Math.Sign(a0) != Math.Sign(a1))
            {
                // Bisect between t0 and t1 (ordered in time).
                var (lo, hi) = dir > 0 ? (t0, t1) : (t1, t0);
                var aLo = alt(lo);
                for (var k = 0; k < 16; k++)
                {
                    var mid = lo + (hi - lo) / 2;
                    var aMid = alt(mid);
                    if (Math.Sign(aMid) == Math.Sign(aLo)) { lo = mid; aLo = aMid; } else hi = mid;
                }
                var when = lo + (hi - lo) / 2;
                return (when, aLo < 0); // below before the crossing → sunrise
            }
            t0 = t1;
            a0 = a1;
        }
        return null;
    }
}

/// <summary>Planetary hours as an influence: computed, no external data.</summary>
public sealed class PlanetaryHoursProvider(IEphemeris ephemeris) : ISignalProvider
{
    public const string Id = "planetary-hours";

    public static readonly InfluenceInfo Influence = new(
        Id,
        "Ore planetarie",
        "Il giorno e la notte divisi ciascuno in 12 ore disuguali, dall'alba al tramonto e dal tramonto all'alba nel luogo " +
        "attuale, governate dai sette pianeti in ordine caldeo; il giorno appartiene al pianeta della sua prima ora " +
        "(Vettio Valente, Picatrix, Lilly). La medicina medievale sceglieva così l'ora dei rimedi.",
        "planetary-hours",
        ProviderKinds.Computed,
        "Dendera, sulle effemeridi Astronomy Engine",
        "https://en.wikipedia.org/wiki/Planetary_hours",
        "Calcolo, nessun dato esterno",
        Reliabilities.Primary,
        [new(SignalKinds.PlanetaryHour, "Pianeta che governa l'ora e il giorno in corso (ore disuguali dall'alba)")]);

    public InfluenceInfo Info => Influence;
    public string Source => Id;
    public TimeSpan SampleValidity => TimeSpan.Zero;

    public Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        var signals = new List<Signal>();
        foreach (var at in query.Frames()) signals.AddRange(At(at, query.Latitude, query.Longitude));
        return Task.FromResult<IReadOnlyList<Signal>>(signals);
    }

    public IReadOnlyList<Signal> At(DateTimeOffset at, double latitude, double longitude)
    {
        if (PlanetaryHours.At(ephemeris, at, latitude, longitude) is not { } h) return [];
        return
        [
            new Signal(
                "planetary-hour", SignalKinds.PlanetaryHour, Id, at, 1.0, h.Number, "ora",
                new Dictionary<string, string>
                {
                    ["ruler"] = h.Ruler, ["dayRuler"] = h.DayRuler, ["hour"] = h.Number.ToString(),
                    ["light"] = h.Day ? "day" : "night",
                },
                "Ora planetaria",
                $"{h.Number}ª ora ({(h.Day ? "del giorno" : "della notte")}) di {Lexicon.Name(h.Ruler)}, giorno di {Lexicon.Name(h.DayRuler)}"),
        ];
    }
}

/// <summary>
/// The Moon's stations: the 28 Arabic mansions (tropical, equal, from 0° Aries; Agrippa, De occulta philosophia
/// II.33, after Picatrix I.4) and the 27 Indian nakshatras (sidereal, Lahiri ayanamsha, 13°20′ each, four padas).
/// </summary>
public static class LunarStations
{
    public static readonly IReadOnlyList<string> Mansions =
    [
        "alnath", "allothaim", "achaomazon", "aldebaran-mansion", "alchatay", "alhanna", "aldimiach", "alnaza",
        "archaam", "algelioche", "azobra", "alzarpha", "alhaire", "achureth", "agrapha", "azubene", "alchil",
        "alchas", "allatha", "abnahaya", "abeda", "sadahacha", "sadabath", "sadabeth", "sadalabra", "alpharg",
        "alcharya", "albotham",
    ];

    public static readonly IReadOnlyList<string> Nakshatras =
    [
        "ashvini", "bharani", "krittika", "rohini", "mrigashira", "ardra", "punarvasu", "pushya", "ashlesha",
        "magha", "purva-phalguni", "uttara-phalguni", "hasta", "chitra", "swati", "vishakha", "anuradha", "jyeshtha",
        "mula", "purva-ashadha", "uttara-ashadha", "shravana", "dhanishta", "shatabhisha", "purva-bhadrapada",
        "uttara-bhadrapada", "revati",
    ];

    /// <summary>Vimshottari lords of the nakshatras, repeating every nine (BPHS).</summary>
    public static readonly IReadOnlyList<string> DashaLords =
        ["ketu", Bodies.Venus, Bodies.Sun, Bodies.Moon, Bodies.Mars, "rahu", Bodies.Jupiter, Bodies.Saturn, Bodies.Mercury];

    /// <summary>Vimshottari years of each lord (120 in all).</summary>
    public static readonly IReadOnlyDictionary<string, double> DashaYears = new Dictionary<string, double>
    {
        ["ketu"] = 7, [Bodies.Venus] = 20, [Bodies.Sun] = 6, [Bodies.Moon] = 10, [Bodies.Mars] = 7,
        ["rahu"] = 18, [Bodies.Jupiter] = 16, [Bodies.Saturn] = 19, [Bodies.Mercury] = 17,
    };

    public const double NakshatraWidth = 360.0 / 27;

    /// <summary>
    /// Lahiri ayanamsha in degrees: 23°51′ at J2000 plus 50.29″ a year (Indian Calendar Reform Committee, 1955);
    /// linear, a few arcminutes off over 1900–2100.
    /// </summary>
    public static double Ayanamsha(DateTimeOffset at) =>
        23.85 + (at - new DateTimeOffset(2000, 1, 1, 12, 0, 0, TimeSpan.Zero)).TotalDays / 365.25 * (50.29 / 3600);

    public static double Sidereal(double tropical, DateTimeOffset at) => Zodiac.Norm(tropical - Ayanamsha(at));

    public static int MansionOf(double tropical) => (int)(Zodiac.Norm(tropical) / (360.0 / 28)) % 28;

    public static (int Index, int Pada, double Fraction) NakshatraOf(double sidereal)
    {
        var pos = Zodiac.Norm(sidereal) / NakshatraWidth;
        var index = (int)pos % 27;
        var fraction = pos - Math.Floor(pos);
        return (index, (int)(fraction * 4) + 1, fraction);
    }
}

/// <summary>The Moon's mansion and nakshatra as an influence: computed, no external data.</summary>
public sealed class LunarStationsProvider(IEphemeris ephemeris) : ISignalProvider
{
    public const string Id = "lunar-stations";

    public static readonly InfluenceInfo Influence = new(
        Id,
        "Dimore lunari e zodiaco siderale",
        "Le stazioni della Luna nel suo mese: le 28 dimore arabe (manāzil, zodiaco tropicale, da Picatrix e Agrippa) e i " +
        "27 nakshatra indiani (zodiaco siderale, ayanamsha di Lahiri), con il pada e il pianeta che governa il nakshatra; " +
        "e il segno siderale dei sette pianeti e dei nodi Rahu e Ketu, come li legge l'astrologia indiana.",
        "lunar-stations",
        ProviderKinds.Computed,
        "Dendera, sulle effemeridi Astronomy Engine",
        "https://en.wikipedia.org/wiki/Lunar_mansion",
        "Calcolo, nessun dato esterno",
        Reliabilities.Primary,
        [
            new(SignalKinds.LunarMansion, "Dimora araba (1–28) in cui si trova la Luna"),
            new(SignalKinds.Nakshatra, "Nakshatra (1–27) e pada della Luna nello zodiaco siderale, con il suo signore"),
            new(SignalKinds.SiderealPlacement, "Segno siderale e nakshatra dei sette pianeti e di Rahu e Ketu (nodi lunari)"),
        ]);

    public InfluenceInfo Info => Influence;
    public string Source => Id;
    public TimeSpan SampleValidity => TimeSpan.Zero;

    public Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        var signals = new List<Signal>();
        foreach (var at in query.Frames()) signals.AddRange(At(at));
        return Task.FromResult<IReadOnlyList<Signal>>(signals);
    }

    public IReadOnlyList<Signal> At(DateTimeOffset at)
    {
        var moon = ephemeris.Position(Bodies.Moon, at).Longitude;
        var m = LunarStations.MansionOf(moon);
        var sidereal = LunarStations.Sidereal(moon, at);
        var (n, pada, _) = LunarStations.NakshatraOf(sidereal);
        var lord = LunarStations.DashaLords[n % 9];
        return
        [
            new Signal("lunar-mansion", SignalKinds.LunarMansion, Id, at, 1.0, m + 1, "dimora",
                new Dictionary<string, string> { ["mansion"] = LunarStations.Mansions[m], ["number"] = (m + 1).ToString() },
                "Dimora lunare", $"{m + 1}ª dimora, {Lexicon.Name(LunarStations.Mansions[m])} (Luna a {Zodiac.Format(moon)})"),
            new Signal("nakshatra", SignalKinds.Nakshatra, Id, at, 1.0, n + 1, "nakshatra",
                new Dictionary<string, string>
                {
                    ["nakshatra"] = LunarStations.Nakshatras[n], ["number"] = (n + 1).ToString(), ["pada"] = pada.ToString(), ["lord"] = lord,
                },
                "Nakshatra della Luna",
                $"{Lexicon.Name(LunarStations.Nakshatras[n])}, pada {pada}, signore {Lexicon.Name(lord)} (Luna siderale {sidereal:0.0}°)"),
            .. SiderealPlacements(at),
        ];
    }

    /// <summary>The nine grahas of Jyotish on the sidereal zodiac: the seven planets, Rahu (mean north node), Ketu (opposite).</summary>
    private IEnumerable<Signal> SiderealPlacements(DateTimeOffset at)
    {
        var bodies = new[] { Bodies.Sun, Bodies.Moon, Bodies.Mars, Bodies.Mercury, Bodies.Jupiter, Bodies.Venus, Bodies.Saturn }
            .Select(b => (graha: b, lon: ephemeris.Position(b, at).Longitude))
            .ToList();
        var node = ephemeris.Position(Bodies.NorthNode, at).Longitude;
        bodies.Add(("rahu", node));
        bodies.Add(("ketu", node + 180));
        foreach (var (graha, tropical) in bodies)
        {
            var sidereal = LunarStations.Sidereal(tropical, at);
            var sign = Zodiac.SignOf(sidereal);
            var (n, _, _) = LunarStations.NakshatraOf(sidereal);
            yield return new Signal($"sidereal:{graha}", SignalKinds.SiderealPlacement, Id, at, 1.0, Math.Round(sidereal, 2), "°",
                new Dictionary<string, string> { ["body"] = graha, ["sign"] = sign, ["nakshatra"] = LunarStations.Nakshatras[n] },
                $"{Lexicon.Name(graha)} siderale", $"{Lexicon.Name(graha)} in {Lexicon.Name(sign)} siderale, {Lexicon.Name(LunarStations.Nakshatras[n])}");
        }
    }
}

/// <summary>
/// Times of a life, from the birth chart: septennials (Solon; Steiner), annual profections (Vettius Valens,
/// Anthology IV; whole signs, traditional rulers) and Vimshottari dashas (Parashara, BPHS).
/// </summary>
public static class LifeTimes
{
    public const double YearDays = 365.25;

    /// <summary>Traditional sign rulers (no outer planets).</summary>
    public static readonly IReadOnlyDictionary<string, string> Rulers = new Dictionary<string, string>
    {
        ["aries"] = Bodies.Mars, ["taurus"] = Bodies.Venus, ["gemini"] = Bodies.Mercury, ["cancer"] = Bodies.Moon,
        ["leo"] = Bodies.Sun, ["virgo"] = Bodies.Mercury, ["libra"] = Bodies.Venus, ["scorpio"] = Bodies.Mars,
        ["sagittarius"] = Bodies.Jupiter, ["capricorn"] = Bodies.Saturn, ["aquarius"] = Bodies.Saturn, ["pisces"] = Bodies.Jupiter,
    };

    public static double AgeYears(DateTimeOffset birth, DateTimeOffset at) => (at - birth).TotalDays / YearDays;

    public sealed record Profection(int Age, int House, string Sign, string Lord);

    public static Profection ProfectionAt(NatalChart natal, DateTimeOffset at)
    {
        var age = Math.Max(0, (int)Math.Floor(AgeYears(natal.Birth.BirthUtc, at)));
        var asc = natal.Point(Bodies.Ascendant)!.Sign;
        var sign = Zodiac.Signs[(Zodiac.Signs.ToList().IndexOf(asc) + age) % 12];
        return new Profection(age, age % 12 + 1, sign, Rulers[sign]);
    }

    public sealed record Dasha(string Lord, string SubLord, DateTimeOffset Start, DateTimeOffset End);

    /// <summary>Mahadasha and antardasha in force at an instant (null before birth or after 120 years).</summary>
    public static Dasha? DashaAt(NatalChart natal, DateTimeOffset at)
    {
        var birth = natal.Birth.BirthUtc;
        if (at < birth) return null;
        var moon = LunarStations.Sidereal(natal.Point(Bodies.Moon)!.Longitude, birth);
        var (n, _, fraction) = LunarStations.NakshatraOf(moon);
        var lords = LunarStations.DashaLords;
        var first = n % 9;
        // The first period is already partly spent at birth: it started before, as far back as the Moon had come.
        var start = birth.AddDays(-fraction * LunarStations.DashaYears[lords[first]] * YearDays);
        for (var k = 0; k < 18; k++)
        {
            var lord = lords[(first + k) % 9];
            var years = LunarStations.DashaYears[lord];
            var end = start.AddDays(years * YearDays);
            if (at < end)
            {
                var subStart = start;
                for (var j = 0; j < 9; j++)
                {
                    var sub = lords[(first + k + j) % 9];
                    var subEnd = subStart.AddDays(years * LunarStations.DashaYears[sub] / 120 * YearDays);
                    if (at < subEnd) return new Dasha(lord, sub, subStart, subEnd);
                    subStart = subEnd;
                }
                return new Dasha(lord, lord, start, end);
            }
            start = end;
        }
        return null;
    }
}

/// <summary>Times of the person's life as an influence: needs the natal chart, computed, no external data.</summary>
public sealed class LifeTimesProvider : ISignalProvider
{
    public const string Id = "life-times";

    public static readonly InfluenceInfo Influence = new(
        Id,
        "Tempi della vita",
        "I ritmi della tua vita calcolati dalla nascita: i settenni (periodi di sette anni, da Solone a Steiner), la " +
        "profezione annuale (ogni anno l'Ascendente avanza di un segno e il suo pianeta diventa signore dell'anno, " +
        "Vettio Valente) e i dasha vimshottari dell'astrologia indiana (Parashara), dalla Luna siderale di nascita.",
        "life-times",
        ProviderKinds.Computed,
        "Dendera, sul tema natale",
        "https://en.wikipedia.org/wiki/Profection",
        "Calcolo, nessun dato esterno",
        Reliabilities.Primary,
        [
            new(SignalKinds.Septennial, "Settennio della vita in corso e anno al suo interno"),
            new(SignalKinds.Profection, "Profezione annuale: casa e segno attivati quest'anno e il loro signore"),
            new(SignalKinds.Dasha, "Mahadasha e antardasha vimshottari in corso"),
        ]);

    public InfluenceInfo Info => Influence;
    public string Source => Id;
    public TimeSpan SampleValidity => TimeSpan.Zero;

    public Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        var signals = new List<Signal>();
        if (query.Natal is { } natal)
            foreach (var at in query.Frames()) signals.AddRange(At(at, natal));
        return Task.FromResult<IReadOnlyList<Signal>>(signals);
    }

    public static IReadOnlyList<Signal> At(DateTimeOffset at, NatalChart natal)
    {
        var age = LifeTimes.AgeYears(natal.Birth.BirthUtc, at);
        if (age < 0) return [];
        var signals = new List<Signal>(3);

        var septennial = (int)(age / 7) + 1;
        var year = (int)(age % 7) + 1;
        signals.Add(new Signal("septennial", SignalKinds.Septennial, Id, at, 1.0, Math.Round(age, 2), "anni",
            new Dictionary<string, string> { ["septennial"] = septennial.ToString(), ["year"] = year.ToString() },
            "Settennio", $"{septennial}° settennio ({(septennial - 1) * 7}–{septennial * 7} anni), {year}° anno"));

        var p = LifeTimes.ProfectionAt(natal, at);
        signals.Add(new Signal("profection", SignalKinds.Profection, Id, at, 1.0, p.House, "casa",
            new Dictionary<string, string> { ["house"] = p.House.ToString(), ["sign"] = p.Sign, ["lord"] = p.Lord },
            "Profezione annuale", $"{p.Age} anni: casa {p.House} in {Lexicon.Name(p.Sign)}, signore dell'anno {Lexicon.Name(p.Lord)}"));

        if (LifeTimes.DashaAt(natal, at) is { } d)
        {
            signals.Add(new Signal("dasha", SignalKinds.Dasha, Id, at, 1.0, null, null,
                new Dictionary<string, string> { ["lord"] = d.Lord, ["subLord"] = d.SubLord },
                "Dasha vimshottari",
                $"{Lexicon.Name(d.Lord)}–{Lexicon.Name(d.SubLord)} fino al {d.End:yyyy-MM-dd}"));
        }
        return signals;
    }
}
