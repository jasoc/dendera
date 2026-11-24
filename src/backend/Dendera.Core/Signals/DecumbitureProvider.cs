using Dendera.Core.Astro;

namespace Dendera.Core.Signals;

/// <summary>
/// Decumbiture: the course of an illness counted from the moment it began (an onset recorded in the journal).
/// Two facts, read by different traditions: the days since onset with the Hippocratic critical days (Prognostic 20:
/// the 4th, 7th, 11th, 14th, 17th, 20th), and the Moon's aspects to the place it held at the onset (Culpeper,
/// Astrological Judgement of Diseases, 1655: squares, opposition and return are crises, the semisquares and
/// sesquiquadrates indicative "judicial" days). Pure: the onsets come in with the query.
/// </summary>
public sealed class DecumbitureProvider(IEphemeris ephemeris) : ISignalProvider
{
    public const string Id = "decumbiture";

    /// <summary>An episode is followed for this long after its onset.</summary>
    public const int FollowDays = 30;

    /// <summary>The Moon's moiety (al-Bīrūnī, 12° / 2).</summary>
    public const double Orb = 6;

    public static readonly IReadOnlySet<int> CriticalDays = new HashSet<int> { 4, 7, 11, 14, 17, 20 };

    public static readonly InfluenceInfo Influence = new(
        Id,
        "Decombitura",
        "Il decorso di un malessere contato dal momento in cui è cominciato, se lo segni nel diario: i giorni passati con " +
        "i giorni critici di Ippocrate (4°, 7°, 11°, 14°, 17°, 20°), e gli aspetti della Luna al punto in cui si trovava " +
        "all'inizio, che Culpeper usava per prevedere crisi e giorni indicativi.",
        "decumbiture",
        ProviderKinds.Computed,
        "Dendera, sul diario e sulle effemeridi",
        "https://en.wikipedia.org/wiki/Decumbiture",
        "Calcolo, nessun dato esterno",
        Reliabilities.Primary,
        [
            new(SignalKinds.IllnessDay, "Giorno del malessere dal suo inizio (fino al 30°), con i giorni critici di Ippocrate"),
            new(SignalKinds.DecumbitureMoon, "La Luna in quadratura, opposizione o ritorno (crisi) o a 45°/135° (giorni indicativi) dal punto dell'inizio"),
        ]);

    private static readonly (double Angle, string Aspect, string Phase)[] MoonAspects =
    [
        (45, "semisquare", "judicial"), (90, "square", "crisis"), (135, "sesquiquadrate", "judicial"),
        (180, "opposition", "crisis"), (0, "conjunction", "crisis"),
    ];

    public InfluenceInfo Info => Influence;
    public string Source => Id;
    public TimeSpan SampleValidity => TimeSpan.Zero;

    public Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        var signals = new List<Signal>();
        if (query.IllnessOnsets is { Count: > 0 } onsets)
            foreach (var at in query.Frames()) signals.AddRange(At(at, onsets));
        return Task.FromResult<IReadOnlyList<Signal>>(signals);
    }

    public IReadOnlyList<Signal> At(DateTimeOffset at, IReadOnlyList<DateTimeOffset> onsets)
    {
        var signals = new List<Signal>();
        var moonNow = ephemeris.Position(Bodies.Moon, at).Longitude;
        foreach (var onset in onsets.Where(o => o <= at && at - o < TimeSpan.FromDays(FollowDays)))
        {
            var day = (int)((at - onset).TotalDays) + 1;
            var critical = CriticalDays.Contains(day);
            var key = onset.ToUnixTimeSeconds();
            signals.Add(new Signal($"illness-day:{key}", SignalKinds.IllnessDay, Id, at, 1.0, day, "giorno",
                new Dictionary<string, string> { ["day"] = day.ToString(), ["critical"] = critical ? "true" : "false" },
                "Giorno del malessere",
                $"{day}° giorno dall'inizio ({onset:yyyy-MM-dd HH:mm} UTC){(critical ? ", giorno critico" : "")}"));

            if (at - onset < TimeSpan.FromDays(1.5)) continue; // the onset itself is not a return
            var moonThen = ephemeris.Position(Bodies.Moon, onset).Longitude;
            var sep = Zodiac.Separation(moonNow, moonThen);
            foreach (var (angle, aspect, phase) in MoonAspects)
            {
                var dev = Math.Abs(sep - angle);
                if (dev > Orb) continue;
                signals.Add(new Signal($"decumbiture-moon:{key}", SignalKinds.DecumbitureMoon, Id, at,
                    Math.Cos(dev / Orb * Math.PI / 2), Math.Round(dev, 2), "° orb",
                    new Dictionary<string, string>
                    {
                        ["moonAspect"] = aspect, ["phase"] = phase, ["sign"] = Zodiac.SignOf(moonNow), ["day"] = day.ToString(),
                    },
                    "Luna dalla decombitura",
                    $"Luna in {Lexicon.Name(aspect)} col suo punto d'inizio ({Lexicon.Name(phase)}), {day}° giorno"));
                break;
            }
        }
        return signals;
    }
}
