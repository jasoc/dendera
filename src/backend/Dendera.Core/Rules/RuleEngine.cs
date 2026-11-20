using System.Text.RegularExpressions;
using Dendera.Core.Astro;
using Dendera.Core.Signals;

namespace Dendera.Core.Rules;

public sealed record ZoneContribution(
    string SchoolId,
    string RuleId,
    string SignalKey,
    string SignalLabel,
    string Quality,
    double Amount,
    string Text);

public sealed record ZoneActivation(
    string Zone,
    string Label,
    string Layer,
    bool Systemic,
    double Score, // saturated activation 0..1 = 1 - e^(-raw/saturation)

    double Raw,
    string? DominantQuality,
    IReadOnlyDictionary<string, double> ByQuality, // raw sum per quality
    IReadOnlyDictionary<string, double> QualityScores, // saturated 0..1 per quality, same formula as Score
    double NatalBaseline,
    IReadOnlyList<ZoneContribution> Contributions,
    IReadOnlyDictionary<string, double> BySchool); // school id → that school's score

/// <summary>Pure: signals + one school (+ natal baseline) → body zone activations with explanations.</summary>
public static partial class RuleEngine
{
    /// <summary>
    /// Two passes. First, rules on signals from the influences. Then, if the school has rules of kind
    /// <see cref="SignalKinds.Node"/>, every node it activated becomes an internal signal (intensity = its score,
    /// quality = its dominant quality) and those rules run once on them: the school's own relations between
    /// nodes ("liver → anger"). One pass only, so no cycles.
    /// </summary>
    public static IReadOnlyList<ZoneActivation> Evaluate(
        RuleSet rules,
        IReadOnlyList<Signal> signals,
        IReadOnlyDictionary<string, double>? natalBaseline = null)
    {
        var contributions = new Dictionary<string, List<ZoneContribution>>();

        void Apply(IEnumerable<Signal> source, bool nodeRules)
        {
            foreach (var signal in source)
            foreach (var rule in rules.Rules)
            {
                if ((rule.When.Kind == SignalKinds.Node) != nodeRules || !Matches(rule.When, signal)) continue;

                var quality = rule.Effect.Quality ?? signal.Tag("quality") ?? "neutral";
                var amount = rule.Effect.Weight * signal.Intensity * TagWeight(rule.Effect, signal);
                if (amount <= 0) continue;

                var text = Render(rule.Text, signal);
                foreach (var zone in ZonesFor(rules, rule.Effect, signal))
                {
                    var baseline = natalBaseline?.GetValueOrDefault(zone) ?? 0;
                    var amplified = amount * (1 + rules.NatalAmplification * baseline);
                    if (!contributions.TryGetValue(zone, out var list)) contributions[zone] = list = [];
                    list.Add(new ZoneContribution(rules.Id, rule.Id, signal.Key, signal.Label, quality, amplified, text));
                }
            }
        }

        Apply(signals, nodeRules: false);
        if (rules.Rules.Any(r => r.When.Kind == SignalKinds.Node))
        {
            var at = signals.Count > 0 ? signals[0].At : default;
            var nodes = Build(rules, contributions, natalBaseline)
                .Where(z => z.Score > 0)
                .Select(z => new Signal(
                    $"node:{z.Zone}", SignalKinds.Node, "body", at, z.Score, Math.Round(z.Score * 100), "%",
                    new Dictionary<string, string> { ["node"] = z.Zone, ["quality"] = z.DominantQuality ?? "neutral" },
                    z.Label, $"{Math.Round(z.Score * 100)}%"))
                .ToList();
            Apply(nodes, nodeRules: true);
        }

        return Build(rules, contributions, natalBaseline);
    }

    private static IReadOnlyList<ZoneActivation> Build(
        RuleSet rules,
        Dictionary<string, List<ZoneContribution>> contributions,
        IReadOnlyDictionary<string, double>? natalBaseline) =>
        BodyZones.Ordered.Select(zone =>
        {
            var list = contributions.GetValueOrDefault(zone) ?? [];
            var raw = list.Sum(c => c.Amount);
            var byQuality = list.GroupBy(c => c.Quality).ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));
            var dominant = byQuality.Count == 0 ? null : byQuality.MaxBy(kv => kv.Value).Key;
            var score = 1 - Math.Exp(-raw / rules.Saturation);
            return new ZoneActivation(
                zone,
                Lexicon.Name(zone),
                BodyZones.LayerOf(zone),
                BodyZones.Systemic.Contains(zone),
                score,
                raw,
                dominant,
                byQuality,
                byQuality.ToDictionary(kv => kv.Key, kv => 1 - Math.Exp(-kv.Value / rules.Saturation)),
                natalBaseline?.GetValueOrDefault(zone) ?? 0,
                list.OrderByDescending(c => c.Amount).ToList(),
                new Dictionary<string, double> { [rules.Id] = score });
        }).ToList();

    /// <summary>
    /// Melothesia natal emphasis per zone (0..1): which zones the birth chart makes "sensitive".
    /// Luminaries and the Ascendant weigh most; each point marks its sign's zones and its own zones.
    /// </summary>
    public static IReadOnlyDictionary<string, double> NatalBaseline(RuleSet rules, NatalChart chart)
    {
        var acc = new Dictionary<string, double>();
        void Add(IEnumerable<string> zones, double w)
        {
            foreach (var z in zones) acc[z] = acc.GetValueOrDefault(z) + w;
        }

        foreach (var p in chart.Points)
        {
            var weight = p.Id switch
            {
                Bodies.Sun or Bodies.Moon or Bodies.Ascendant => 1.0,
                Bodies.Mercury or Bodies.Venus or Bodies.Mars => 0.6,
                Bodies.Jupiter or Bodies.Saturn => 0.4,
                Bodies.Midheaven => 0.3,
                _ => 0.25,
            };
            Add(rules.ZonesOf(p.Sign), weight);
            Add(rules.ZonesOf(p.Id), weight * 0.5);
        }

        var max = acc.Count == 0 ? 1 : acc.Values.Max();
        return acc.ToDictionary(kv => kv.Key, kv => kv.Value / max);
    }

    private static bool Matches(RuleCondition when, Signal signal)
    {
        if (!string.Equals(when.Kind, signal.Kind, StringComparison.Ordinal)) return false;
        if (signal.Intensity < when.MinIntensity) return false;
        if (when.Tags is null) return true;
        foreach (var (tag, accepted) in when.Tags)
        {
            var value = signal.Tag(tag);
            if (value is null || !(accepted.Contains(value) || accepted.Contains("*"))) return false;
        }
        return true;
    }

    private static double TagWeight(RuleEffect effect, Signal signal)
    {
        if (effect.TagWeights is null) return 1;
        var w = 1.0;
        foreach (var (tag, weights) in effect.TagWeights)
        {
            var value = signal.Tag(tag);
            if (value is not null && weights.TryGetValue(value, out var m)) w *= m;
            else if (weights.TryGetValue("*", out var d)) w *= d;
        }
        return w;
    }

    private static IEnumerable<string> ZonesFor(RuleSet rules, RuleEffect effect, Signal signal)
    {
        var zones = new HashSet<string>(effect.Zones ?? []);
        foreach (var tag in effect.ZonesFrom ?? [])
        {
            var value = signal.Tag(tag);
            if (value is not null) zones.UnionWith(rules.ZonesOf(value));
        }
        return zones;
    }

    private static string Render(string template, Signal signal)
    {
        if (string.IsNullOrEmpty(template)) return signal.Label;
        return Placeholder().Replace(template, m =>
        {
            var name = m.Groups[1].Value;
            if (name == "label") return signal.Label;
            if (name == "detail") return signal.Detail;
            var v = signal.Tag(name);
            return v is null ? m.Value : Lexicon.Name(v);
        });
    }

    [GeneratedRegex(@"\{([a-zA-Z]+)\}")]
    private static partial Regex Placeholder();
}
