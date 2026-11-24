using Dendera.Core.Astro;

namespace Dendera.Core.Signals;

/// <summary>
/// Pure arithmetic of the Chinese medical calendar: the 24 solar terms (jieqi) and the five-phase seasons from the
/// Sun's ecliptic longitude, and the 24-hour organ clock from local solar time.
/// </summary>
public static class TcmCalendar
{
    /// <summary>The 24 jieqi in 15° steps of solar longitude, starting at Lichun (315°).</summary>
    public static readonly IReadOnlyList<string> Terms =
    [
        "lichun", "yushui", "jingzhe", "chunfen", "qingming", "guyu",
        "lixia", "xiaoman", "mangzhong", "xiazhi", "xiaoshu", "dashu",
        "liqiu", "chushu", "bailu", "qiufen", "hanlu", "shuangjiang",
        "lidong", "xiaoxue", "daxue", "dongzhi", "xiaohan", "dahan",
    ];

    /// <summary>Two-hour peaks of the twelve organs, starting at 03:00 local solar time.</summary>
    public static readonly IReadOnlyList<string> OrganClockOrder =
    [
        "lung", "large-intestine", "stomach", "spleen", "heart", "small-intestine",
        "bladder", "kidney", "pericardium", "triple-burner", "gallbladder", "liver",
    ];

    public static readonly IReadOnlyDictionary<string, string> ElementOfOrgan = new Dictionary<string, string>
    {
        ["liver"] = "wood", ["gallbladder"] = "wood",
        ["heart"] = "fire", ["small-intestine"] = "fire", ["pericardium"] = "fire", ["triple-burner"] = "fire",
        ["spleen"] = "earth", ["stomach"] = "earth",
        ["lung"] = "metal", ["large-intestine"] = "metal",
        ["kidney"] = "water", ["bladder"] = "water",
    };

    public sealed record Season(string Term, string SeasonId, string Element);

    /// <summary>Solar term and five-phase season for a solar longitude (degrees).</summary>
    public static Season SeasonOf(double sunLongitude)
    {
        var lon = Zodiac.Norm(sunLongitude);
        var term = Terms[(int)(Zodiac.Norm(lon - 315) / 15) % 24];

        // Late summer (earth): the ~18 days before each of the four "Li" terms (start of a season).
        if (lon is >= 297 and < 315 or >= 27 and < 45 or >= 117 and < 135 or >= 207 and < 225)
            return new Season(term, "late-summer", "earth");

        return Zodiac.Norm(lon - 315) switch
        {
            < 90 => new Season(term, "spring", "wood"),
            < 180 => new Season(term, "summer", "fire"),
            < 270 => new Season(term, "autumn", "metal"),
            _ => new Season(term, "winter", "water"),
        };
    }

    /// <summary>Local mean solar time in hours (0..24): UTC + longitude / 15.</summary>
    public static double LocalSolarHours(DateTimeOffset at, double longitude)
    {
        var u = at.UtcDateTime;
        var h = u.Hour + u.Minute / 60.0 + u.Second / 3600.0 + longitude / 15.0;
        return ((h % 24) + 24) % 24;
    }

    public static readonly IReadOnlyList<string> Stems =
        ["stem-jia", "stem-yi", "stem-bing", "stem-ding", "stem-wu", "stem-ji", "stem-geng", "stem-xin", "stem-ren", "stem-gui"];
    public static readonly IReadOnlyList<string> Branches =
        ["branch-zi", "branch-chou", "branch-yin", "branch-mao", "branch-chen", "branch-si", "branch-wu", "branch-wei",
         "branch-shen", "branch-you", "branch-xu", "branch-hai"];
    /// <summary>Element of each heavenly stem (two per element: wood, fire, earth, metal, water).</summary>
    public static string StemElement(int stem) => new[] { "wood", "fire", "earth", "metal", "water" }[stem / 2];

    public sealed record Pillars(int Year, int Day, int HourBranch);

    /// <summary>
    /// Sexagenary year, day and hour: day index = (JDN + 49) mod 60 (0 = jiazi; 1 Jan 2000 = wuwu, 54), the day
    /// changing at 23:00 local solar time (start of the zi hour); hour branch every two hours from 23:00; the year
    /// changes at Lichun (Sun at 315°), index (year − 4) mod 60.
    /// </summary>
    public static Pillars PillarsAt(DateTimeOffset at, double longitude, double sunLongitude)
    {
        var local = at.UtcDateTime.AddHours(longitude / 15);
        var dayDate = local.AddHours(1).Date; // from 23:00 it is already the next day
        var jdn = (int)(dayDate - new DateTime(2000, 1, 1)).TotalDays + 2451545;
        var day = ((jdn + 49) % 60 + 60) % 60;
        var hours = LocalSolarHours(at, longitude);
        var hourBranch = (int)((hours + 1) / 2) % 12;
        var year = local.Year;
        var sun = Zodiac.Norm(sunLongitude);
        if (local.Month <= 2 && sun is >= 270 and < 315) year -= 1; // before Lichun: still last year
        return new Pillars(((year - 4) % 60 + 60) % 60, day, hourBranch);
    }

    /// <summary>The organ at its two-hour peak at an instant and longitude.</summary>
    public static string OrganAt(DateTimeOffset at, double longitude)
    {
        var h = LocalSolarHours(at, longitude);
        return OrganClockOrder[(int)(((h - 3 + 24) % 24) / 2) % 12];
    }
}

/// <summary>The Chinese medical calendar as an influence: computed, no external data.</summary>
public sealed class TcmCalendarProvider(IEphemeris ephemeris) : ISignalProvider
{
    public const string Id = "tcm-calendar";

    public static readonly InfluenceInfo Influence = new(
        Id,
        "Calendario cinese",
        "Il calendario della medicina cinese calcolato dal cielo e dall'orologio: i 24 termini solari con la stagione " +
        "dei cinque elementi (dalla posizione del Sole) e l'orologio dei 12 organi (dall'ora solare del luogo).",
        "tcm-calendar",
        ProviderKinds.Computed,
        "Dendera, sulle effemeridi Astronomy Engine",
        "https://en.wikipedia.org/wiki/Solar_term",
        "Calcolo, nessun dato esterno",
        Reliabilities.Primary,
        [
            new(SignalKinds.SolarTerm, "Termine solare in corso e stagione dei cinque elementi (Legno, Fuoco, Terra, Metallo, Acqua)"),
            new(SignalKinds.OrganClock, "Organo nelle sue due ore di massimo flusso secondo l'ora solare locale"),
            new(SignalKinds.Ganzhi, "Tronchi celesti e rami terrestri dell'anno, del giorno e dell'ora (calendario sessagesimale)"),
        ]);

    public InfluenceInfo Info => Influence;

    public string Source => Id;

    public TimeSpan SampleValidity => TimeSpan.Zero;

    public Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        var signals = new List<Signal>();
        foreach (var at in query.Frames()) signals.AddRange(At(at, query.Longitude));
        return Task.FromResult<IReadOnlyList<Signal>>(signals);
    }

    /// <summary>Solar term and organ clock at one instant and longitude.</summary>
    public IReadOnlyList<Signal> At(DateTimeOffset at, double longitude)
    {
        var signals = new List<Signal>(3);
        {
            var sun = ephemeris.Position(Bodies.Sun, at).Longitude;
            var season = TcmCalendar.SeasonOf(sun);
            signals.Add(new Signal(
                $"solar-term:{season.Element}", SignalKinds.SolarTerm, Id, at, 1.0, Math.Round(sun, 2), "° Sole",
                new Dictionary<string, string> { ["term"] = season.Term, ["season"] = season.SeasonId, ["element"] = season.Element },
                SeasonLabel(season.Element),
                $"{Lexicon.Name(season.Term)}, {Lexicon.Name(season.SeasonId)}, Sole a {sun:0.0}°"));

            var organ = TcmCalendar.OrganAt(at, longitude);
            var hours = TcmCalendar.LocalSolarHours(at, longitude);
            signals.Add(new Signal(
                $"organ-clock:{organ}", SignalKinds.OrganClock, Id, at, 1.0, Math.Round(hours, 2), "h solari",
                new Dictionary<string, string> { ["organ"] = organ, ["element"] = TcmCalendar.ElementOfOrgan[organ] },
                $"Orologio degli organi: {Lexicon.Name(organ)}",
                $"ora solare {(int)hours:00}:{(int)(hours % 1 * 60):00}"));

            var pillars = TcmCalendar.PillarsAt(at, longitude, sun);
            string Name(int i) => $"{Lexicon.Name(TcmCalendar.Stems[i % 10])} {Lexicon.Name(TcmCalendar.Branches[i % 12])}";
            var dayStem = pillars.Day % 10;
            signals.Add(new Signal(
                "ganzhi", SignalKinds.Ganzhi, Id, at, 1.0, pillars.Day + 1, "giorno",
                new Dictionary<string, string>
                {
                    ["yearStem"] = TcmCalendar.Stems[pillars.Year % 10], ["yearBranch"] = TcmCalendar.Branches[pillars.Year % 12],
                    ["dayStem"] = TcmCalendar.Stems[dayStem], ["dayBranch"] = TcmCalendar.Branches[pillars.Day % 12],
                    ["hourBranch"] = TcmCalendar.Branches[pillars.HourBranch],
                    ["dayElement"] = TcmCalendar.StemElement(dayStem),
                    ["yearElement"] = TcmCalendar.StemElement(pillars.Year % 10),
                },
                "Calendario sessagesimale",
                $"anno {Name(pillars.Year)}, giorno {Name(pillars.Day)}, ora {Lexicon.Name(TcmCalendar.Branches[pillars.HourBranch])}"));
        }
        return signals;
    }

    private static string SeasonLabel(string element) => element switch
    {
        "wood" => "Stagione del Legno",
        "fire" => "Stagione del Fuoco",
        "earth" => "Stagione della Terra",
        "metal" => "Stagione del Metallo",
        _ => "Stagione dell'Acqua",
    };
}
