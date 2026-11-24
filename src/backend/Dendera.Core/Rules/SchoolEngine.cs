using Dendera.Core.Astro;
using Dendera.Core.Signals;

namespace Dendera.Core.Rules;

/// <summary>Natal emphasis per zone (0..1), per school id.</summary>
public sealed class NatalBaselines : Dictionary<string, IReadOnlyDictionary<string, double>>
{
    /// <summary>Computes each school's baseline with its own strategy.</summary>
    /// <param name="humanDesign">Defined Human Design centers of the natal chart, computed only when a school needs them.</param>
    public static NatalBaselines For(
        IEnumerable<RuleSet> schools, NatalChart natal, Func<IReadOnlyCollection<string>>? humanDesign = null)
    {
        var result = new NatalBaselines();
        IReadOnlyCollection<string>? centers = null;
        foreach (var school in schools)
        {
            result[school.Id] = school.Baseline switch
            {
                BaselineStrategies.Melothesia => RuleEngine.NatalBaseline(school, natal),
                BaselineStrategies.HumanDesign => FromSymbols(school, centers ??= humanDesign?.Invoke() ?? []),
                _ => new Dictionary<string, double>(),
            };
        }
        return result;
    }

    /// <summary>Every zone of the given symbols (e.g. defined centers) at full emphasis.</summary>
    public static IReadOnlyDictionary<string, double> FromSymbols(RuleSet school, IEnumerable<string> symbols) =>
        symbols.SelectMany(school.ZonesOf).Distinct().ToDictionary(z => z, _ => 1.0);
}

/// <summary>How schools are averaged on a node.</summary>
public enum ConsensusMode
{
    /// <summary>Mean of the schools that cover the node: one vote per school.</summary>
    Plain,
    /// <summary>
    /// One vote per family of traditions (<see cref="SchoolEngine.Families"/>): schools of the same lineage, or borrowing
    /// directly from an active school's lineage, are averaged together first, then the families are averaged.
    /// </summary>
    Lineage,
}

/// <summary>
/// Pure: runs several schools on the same signals and combines them per node by <b>consensus</b>: the combined score is
/// the mean of the scores of the schools that can speak about that node (<see cref="RuleSet.Covers"/>), zeros included.
/// More schools never inflate a node; a node only one school knows keeps that school's score; disagreement shows as a
/// middling mean with a wide <see cref="ZoneActivation.BySchool"/> split. A single school is exactly its own score.
/// </summary>
public static class SchoolEngine
{
    public static IReadOnlyList<ZoneActivation> Evaluate(
        IReadOnlyList<RuleSet> schools,
        IReadOnlyList<Signal> signals,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>? natalBaselines = null,
        ConsensusMode mode = ConsensusMode.Plain) =>
        Combine(schools, EvaluateEach(schools, signals, natalBaselines), mode);

    public static IReadOnlyList<IReadOnlyList<ZoneActivation>> EvaluateEach(
        IReadOnlyList<RuleSet> schools,
        IReadOnlyList<Signal> signals,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>? natalBaselines = null) =>
        schools.Select(s => RuleEngine.Evaluate(s, signals, natalBaselines?.GetValueOrDefault(s.Id))).ToList();

    /// <summary>Combines per-school activations (each list in <see cref="BodyZones"/> order, aligned with <paramref name="schools"/>).</summary>
    public static IReadOnlyList<ZoneActivation> Combine(
        IReadOnlyList<RuleSet> schools, IReadOnlyList<IReadOnlyList<ZoneActivation>> perSchool,
        ConsensusMode mode = ConsensusMode.Plain)
    {
        var families = mode == ConsensusMode.Lineage ? Families(schools) : null;
        if (perSchool.Count == 1) return perSchool[0];
        if (perSchool.Count == 0)
        {
            return BodyZones.Ordered.Select(zone => new ZoneActivation(
                zone, Lexicon.Name(zone), BodyZones.LayerOf(zone), BodyZones.Systemic.Contains(zone), 0, 0, null,
                new Dictionary<string, double>(), new Dictionary<string, double>(), 0, [],
                new Dictionary<string, double>())).ToList();
        }

        return Enumerable.Range(0, perSchool[0].Count).Select(i =>
        {
            var first = perSchool[0][i];
            // The schools that can speak about this node; if none can, it stays at zero.
            var covering = perSchool.Select((list, s) => (index: s, z: list[i]))
                .Where(p => schools[p.index].Covers.Contains(first.Zone))
                .ToList();
            var parts = covering.Select(p => p.z).ToList();
            if (parts.Count == 0) parts = [first with { Score = 0, Raw = 0, BySchool = new Dictionary<string, double>() }];

            // Plain: one vote per school. Lineage: the mean of each family's mean.
            double Mean(Func<ZoneActivation, double> f) =>
                families is null || covering.Count == 0
                    ? parts.Average(f)
                    : covering.GroupBy(p => families[p.index]).Average(g => g.Average(p => f(p.z)));
            var byQuality = parts.SelectMany(p => p.ByQuality)
                .GroupBy(kv => kv.Key)
                .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));
            var qualityScores = byQuality.Keys.ToDictionary(q => q, q => Mean(p => p.QualityScores.GetValueOrDefault(q)));
            return new ZoneActivation(
                first.Zone,
                first.Label,
                first.Layer,
                first.Systemic,
                Mean(p => p.Score),
                parts.Sum(p => p.Raw),
                qualityScores.Count == 0 ? null : qualityScores.MaxBy(kv => kv.Value).Key,
                byQuality,
                qualityScores,
                parts.Max(p => p.NatalBaseline),
                parts.SelectMany(p => p.Contributions).OrderByDescending(c => c.Amount).ToList(),
                parts.SelectMany(p => p.BySchool).ToDictionary(kv => kv.Key, kv => kv.Value));
        }).ToList();
    }

    /// <summary>
    /// Family (a lineage id) each school votes with: its own lineage, or the first of its parents that another active
    /// school belongs to (one step only: no chains, or a school borrowing from many — Human Design — would merge
    /// every tradition into one).
    /// </summary>
    public static string[] Families(IReadOnlyList<RuleSet> schools)
    {
        var present = schools.Select(s => s.Lineage).ToHashSet();
        return schools.Select(s => s.Parents.FirstOrDefault(present.Contains) ?? s.Lineage).ToArray();
    }
}
