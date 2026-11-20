using Dendera.Core.Astro;

namespace Dendera.Core.Signals;

/// <summary>
/// Pure Human Design arithmetic: the Rave mandala (64 gates × 6 lines on the ecliptic), the nine centers, the 36
/// channels, and the natal design (personality at birth + design when the Sun was 88° earlier).
/// Gate order verified against a public gates-by-degree reference (gate 41 from 2°00' Aquarius, 25 from 28°15' Pisces).
/// </summary>
public static class HumanDesign
{
    public const double MandalaStart = 302.0; // gate 41 begins at 2°00' Aquarius
    public const double GateWidth = 360.0 / 64;
    public const double LineWidth = GateWidth / 6;
    public const double DesignArc = 88.0;

    /// <summary>Gates in mandala order, going forward in longitude from <see cref="MandalaStart"/>.</summary>
    public static readonly IReadOnlyList<int> Mandala =
    [
        41, 19, 13, 49, 30, 55, 37, 63, 22, 36, 25, 17, 21, 51, 42, 3, 27, 24, 2, 23, 8, 20, 16, 35, 45, 12, 15, 52, 39, 53, 62, 56,
        31, 33, 7, 4, 29, 59, 40, 64, 47, 6, 46, 18, 48, 57, 32, 50, 28, 44, 1, 43, 14, 34, 9, 5, 26, 11, 10, 58, 38, 54, 61, 60,
    ];

    public static readonly IReadOnlyDictionary<string, int[]> Centers = new Dictionary<string, int[]>
    {
        ["hd-head"] = [64, 61, 63],
        ["hd-ajna"] = [47, 24, 4, 17, 43, 11],
        ["hd-throat"] = [62, 23, 56, 35, 12, 45, 33, 8, 31, 20, 16],
        ["hd-g"] = [7, 1, 13, 25, 46, 2, 15, 10],
        ["hd-ego"] = [21, 40, 26, 51],
        ["hd-sacral"] = [34, 5, 14, 29, 59, 9, 3, 42, 27],
        ["hd-solar-plexus"] = [36, 22, 37, 6, 49, 55, 30],
        ["hd-spleen"] = [48, 57, 44, 50, 32, 28, 18],
        ["hd-root"] = [53, 60, 52, 19, 39, 41, 58, 38, 54],
    };

    public static readonly IReadOnlyList<(int A, int B)> Channels =
    [
        (1, 8), (2, 14), (3, 60), (4, 63), (5, 15), (6, 59), (7, 31), (9, 52), (10, 20), (10, 34), (10, 57), (11, 56),
        (12, 22), (13, 33), (16, 48), (17, 62), (18, 58), (19, 49), (20, 34), (20, 57), (21, 45), (23, 43), (24, 61),
        (25, 51), (26, 44), (27, 50), (28, 38), (29, 46), (30, 41), (32, 54), (34, 57), (35, 36), (37, 40), (39, 55),
        (42, 53), (47, 64),
    ];

    public const string Earth = "earth";
    public const string SouthNode = "south-node";

    /// <summary>The 13 activations of a chart, in the usual order.</summary>
    public static readonly IReadOnlyList<string> ActivationBodies =
    [
        Bodies.Sun, Earth, Bodies.Moon, Bodies.NorthNode, SouthNode, Bodies.Mercury, Bodies.Venus, Bodies.Mars,
        Bodies.Jupiter, Bodies.Saturn, Bodies.Uranus, Bodies.Neptune, Bodies.Pluto,
    ];

    private static readonly Dictionary<int, string> CenterByGate =
        Centers.SelectMany(c => c.Value.Select(g => (g, c.Key))).ToDictionary(x => x.g, x => x.Key);

    public sealed record Activation(string Body, double Longitude, int Gate, int Line);

    public sealed record Chart(
        DateTimeOffset DesignUtc,
        IReadOnlyList<Activation> Personality,
        IReadOnlyList<Activation> Design,
        IReadOnlySet<int> Gates,
        IReadOnlyList<(int A, int B)> DefinedChannels,
        IReadOnlySet<string> DefinedCenters);

    public static (int Gate, int Line) GateAt(double longitude)
    {
        var offset = Zodiac.Norm(longitude - MandalaStart);
        var index = Math.Min(63, (int)(offset / GateWidth));
        var line = Math.Min(6, (int)((offset - index * GateWidth) / LineWidth) + 1);
        return (Mandala[index], line);
    }

    public static string CenterOf(int gate) => CenterByGate[gate];

    /// <summary>The gate that forms a channel with <paramref name="gate"/>, if <paramref name="others"/> contains one.</summary>
    public static IEnumerable<(int A, int B)> ChannelsCompleted(int gate, IReadOnlySet<int> others) =>
        Channels.Where(c => (c.A == gate && others.Contains(c.B)) || (c.B == gate && others.Contains(c.A)));

    /// <summary>Longitudes of the 13 activation points at an instant.</summary>
    public static IReadOnlyList<(string Body, double Longitude)> Points(IEphemeris ephemeris, DateTimeOffset at)
    {
        var sky = ephemeris.Positions(at).ToDictionary(p => p.Body, p => p.Longitude);
        return ActivationBodies.Select(b => (b, b switch
        {
            Earth => Zodiac.Norm(sky[Bodies.Sun] + 180),
            SouthNode => Zodiac.Norm(sky[Bodies.NorthNode] + 180),
            _ => sky[b],
        })).ToList();
    }

    /// <summary>The instant before birth when the Sun was <see cref="DesignArc"/>° behind the natal Sun (bisection).</summary>
    public static DateTimeOffset DesignTime(IEphemeris ephemeris, DateTimeOffset birthUtc)
    {
        var target = Zodiac.Norm(ephemeris.Position(Bodies.Sun, birthUtc).Longitude - DesignArc);
        double F(DateTimeOffset t) => Zodiac.Delta(target, ephemeris.Position(Bodies.Sun, t).Longitude);
        var lo = birthUtc.AddDays(-96);
        var hi = birthUtc.AddDays(-84);
        for (var i = 0; i < 50 && (hi - lo) > TimeSpan.FromSeconds(1); i++)
        {
            var mid = lo + (hi - lo) / 2;
            if (F(mid) < 0) lo = mid; else hi = mid;
        }
        return lo + (hi - lo) / 2;
    }

    public static Chart Compute(IEphemeris ephemeris, DateTimeOffset birthUtc)
    {
        Activation Act((string Body, double Longitude) p)
        {
            var (gate, line) = GateAt(p.Longitude);
            return new Activation(p.Body, p.Longitude, gate, line);
        }

        var design = DesignTime(ephemeris, birthUtc);
        var personality = Points(ephemeris, birthUtc).Select(Act).ToList();
        var designActs = Points(ephemeris, design).Select(Act).ToList();
        var gates = personality.Concat(designActs).Select(a => a.Gate).ToHashSet();
        var channels = Channels.Where(c => gates.Contains(c.A) && gates.Contains(c.B)).ToList();
        var centers = channels.SelectMany(c => new[] { CenterOf(c.A), CenterOf(c.B) }).ToHashSet();
        return new Chart(design, personality, designActs, gates, channels, centers);
    }
}

/// <summary>
/// Human Design transits as an influence: which gate (and center) each body occupies now, and how it meets the
/// natal design (open/defined center, channel completed). Computed from the ephemeris.
/// </summary>
public sealed class HumanDesignProvider(IEphemeris ephemeris) : ISignalProvider
{
    public const string Id = "human-design";

    public static readonly InfluenceInfo Influence = new(
        Id,
        "Mandala di Human Design",
        "Lo zodiaco diviso nelle 64 porte dell'I Ching (6 linee ciascuna): per ogni corpo celeste di oggi, la porta e " +
        "il centro che attiva, se quel centro è aperto o definito nel tuo tema e se il transito completa un tuo canale.",
        "human-design",
        ProviderKinds.Computed,
        "Dendera, sulle effemeridi Astronomy Engine",
        "https://www.jovianarchive.com",
        "Calcolo, nessun dato esterno",
        Reliabilities.Primary,
        [new(SignalKinds.HdGate, "Porta e linea occupate da un corpo in transito, con il centro e l'eventuale canale completato col tema natale")]);

    public InfluenceInfo Info => Influence;

    public string Source => Id;

    public TimeSpan SampleValidity => TimeSpan.Zero;

    public Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        var natal = query.Natal is null ? null : HumanDesign.Compute(ephemeris, query.Natal.Birth.BirthUtc);
        var signals = new List<Signal>();
        foreach (var at in query.Frames())
        foreach (var (body, lon) in HumanDesign.Points(ephemeris, at))
        {
            var (gate, line) = HumanDesign.GateAt(lon);
            var center = HumanDesign.CenterOf(gate);
            var tags = new Dictionary<string, string>
            {
                ["body"] = body, ["gate"] = gate.ToString(), ["line"] = line.ToString(), ["center"] = center,
            };
            var detail = $"porta {gate}.{line}, {Lexicon.Name(center)}";
            if (natal is not null)
            {
                var defined = natal.DefinedCenters.Contains(center);
                tags["natalCenter"] = defined ? "defined" : "open";
                detail += defined ? " (definito nel tema)" : " (aperto nel tema)";
                if (HumanDesign.ChannelsCompleted(gate, natal.Gates).FirstOrDefault() is { A: > 0 } ch)
                {
                    var other = ch.A == gate ? ch.B : ch.A;
                    tags["channel"] = $"{ch.A}-{ch.B}";
                    tags["channelCenter"] = HumanDesign.CenterOf(other);
                    detail += $", completa il canale {ch.A}-{ch.B}";
                }
            }
            signals.Add(new Signal(
                $"hd-gate:{body}", SignalKinds.HdGate, Id, at, 1.0, gate + (line - 1) / 10.0, "porta.linea", tags,
                $"{Lexicon.Name(body)} nel mandala (Human Design)", detail));
        }
        return Task.FromResult<IReadOnlyList<Signal>>(signals);
    }
}
