using Dendera.Core.Astro;
using Dendera.Core.Rules;
using Dendera.Core.Signals;

namespace Dendera.Core.Calibration;

/// <summary>Raw sums of several schools over many reference moments (shared by the ritual and the personal reference).</summary>
public static class ReferenceSampler
{
    /// <summary>
    /// For each school, the nodes it covers (in <see cref="BodyZones.Ordered"/> order) and the raw sum of each node at
    /// every moment. Parallel, written by index: deterministic.
    /// </summary>
    public static IReadOnlyList<(IReadOnlyList<string> Nodes, float[][] Raws)> Raws(
        IReadOnlyList<RuleSet> schools, int count,
        Func<int, (IReadOnlyList<Signal> Signals, NatalBaselines? Baselines)> at)
    {
        var plain = schools.Select(s => s with { Rarity = null }).ToList();
        var nodes = plain.Select(s => (IReadOnlyList<string>)BodyZones.Ordered.Where(s.Covers.Contains).ToList()).ToList();
        var raws = plain.Select((_, k) => nodes[k].Select(_ => new float[count]).ToArray()).ToArray();
        Parallel.For(0, count, i =>
        {
            var (signals, baselines) = at(i);
            for (var k = 0; k < plain.Count; k++)
            {
                var acts = RuleEngine.Evaluate(plain[k], signals, baselines?.GetValueOrDefault(plain[k].Id));
                var byZone = acts.ToDictionary(a => a.Zone, a => a.Raw);
                for (var n = 0; n < nodes[k].Count; n++) raws[k][n][i] = (float)byZone[nodes[k][n]];
            }
        });
        return plain.Select((_, k) => (nodes[k], raws[k])).ToList();
    }
}

/// <summary>
/// The pure influences' signals at one moment, as the API computes them. Every pure provider registered in Program.cs
/// must be here too, or schools reading it are calibrated without its signals.
/// </summary>
public sealed class PureSignals(IEphemeris ephemeris, IReadOnlyList<EclipseEvent> eclipses)
{
    private readonly SkySignalGenerator _sky = new(ephemeris);
    private readonly TcmCalendarProvider _tcm = new(ephemeris);
    private readonly HumanDesignProvider _hd = new(ephemeris);
    private readonly PlanetaryHoursProvider _hours = new(ephemeris);
    private readonly LunarStationsProvider _stations = new(ephemeris);

    public List<Signal> Gather(
        IReadOnlySet<string> needs, DateTimeOffset at, double latitude, double longitude, NatalChart? natal, HumanDesign.Chart? hd)
    {
        var signals = new List<Signal>(128);
        if (needs.Contains(EphemerisProvider.Id)) signals.AddRange(_sky.At(at, new SkyContext(latitude, longitude, natal), eclipses));
        if (needs.Contains(TcmCalendarProvider.Id)) signals.AddRange(_tcm.At(at, longitude));
        if (needs.Contains(HumanDesignProvider.Id)) signals.AddRange(_hd.At(at, hd));
        if (needs.Contains(PlanetaryHoursProvider.Id)) signals.AddRange(_hours.At(at, latitude, longitude));
        if (needs.Contains(LunarStationsProvider.Id)) signals.AddRange(_stations.At(at));
        if (needs.Contains(LifeTimesProvider.Id) && natal is not null) signals.AddRange(LifeTimesProvider.At(at, natal));
        return signals;
    }
}

/// <summary>
/// Personal rarity: a person's own reference, so a score says "more solicited than in N% of <b>your</b> moments".
/// Without it, zones a birth chart always emphasises (e.g. three natal points in one sign, shared by a whole
/// generation) sit above the population at every instant and the score says nothing about today; that constant
/// sensitivity is shown by the natal baseline instead. Built for the schools that read only pure influences (the
/// weather schools keep their climate tables: they do not depend on the chart). Deterministic per birth and place.
/// </summary>
public static class PersonalReference
{
    /// <summary>Moments sampled over the life window (precision of a percentile ≈ ±1/√n: about ±1.6 points at the median).</summary>
    public const int Samples = 4000;

    /// <summary>The life window: from birth, this many years.</summary>
    public const int LifeYears = 90;

    public static bool Applies(RuleSet school) => !ClimateZones.IsClimateDependent(school);

    /// <param name="latitude">Where the person is now (daylight and the hours depend on it).</param>
    public static IReadOnlyDictionary<string, NodeQuantiles> Build(
        IEphemeris ephemeris, IEnumerable<RuleSet> schools, NatalChart natal, HumanDesign.Chart hd, NatalBaselines baselines,
        double latitude, double longitude, int samples = Samples)
    {
        var personal = schools.Where(Applies).ToList();
        if (personal.Count == 0) return new Dictionary<string, NodeQuantiles>();

        var from = natal.Birth.BirthUtc;
        var to = from.AddYears(LifeYears);
        var rng = new Random(Seed(natal.Birth, latitude, longitude));
        var minutes = (to - from).TotalMinutes;
        var instants = Enumerable.Range(0, samples).Select(_ => from.AddMinutes(Math.Floor(rng.NextDouble() * minutes))).ToList();
        var signals = new PureSignals(ephemeris, ephemeris.Eclipses(from.AddDays(-20), to.AddDays(20)));
        var needs = personal.SelectMany(s => s.Requires).ToHashSet();

        var raws = ReferenceSampler.Raws(personal, samples,
            i => (signals.Gather(needs, instants[i], latitude, longitude, natal, hd), baselines));
        return personal.Select((s, k) => (s.Id, table: new NodeQuantiles(
                raws[k].Nodes.Select((n, j) => (n, q: NodeQuantiles.Of(raws[k].Raws[j]))).ToDictionary(x => x.n, x => x.q))))
            .ToDictionary(x => x.Id, x => x.table);
    }

    /// <summary>Stable seed from the birth and the place (not string.GetHashCode, which changes per process).</summary>
    private static int Seed(BirthData birth, double latitude, double longitude)
    {
        unchecked
        {
            var h = 17L;
            h = h * 31 + birth.BirthUtc.UtcTicks;
            h = h * 31 + (long)Math.Round(birth.Latitude * 1e4);
            h = h * 31 + (long)Math.Round(birth.Longitude * 1e4);
            h = h * 31 + (long)Math.Round(latitude * 1e2);
            h = h * 31 + (long)Math.Round(longitude * 1e2);
            return (int)(h ^ (h >> 32));
        }
    }
}
