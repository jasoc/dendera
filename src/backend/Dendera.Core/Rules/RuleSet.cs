using System.Text.Json;
using System.Text.Json.Serialization;
using Dendera.Core.Calibration;
using Dendera.Core.Signals;

namespace Dendera.Core.Rules;

/// <summary>
/// A <b>school</b> (the UI word): one way of reading the influences onto the body. Says which influences it reads
/// (<see cref="Requires"/>), how symbols map to body zones (correspondences) and which rules turn signals into zone
/// activations. Loaded from rules/*.json; several schools can be active at once (see <see cref="SchoolEngine"/>).
/// Rules are hypotheses; the journal + correlation engine tests them.
/// </summary>
public sealed record RuleSet
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";

    /// <summary>Icon id in the frontend registry ("school-melothesia").</summary>
    public string Icon { get; init; } = "";

    /// <summary>Italian, short: which tradition the school comes from.</summary>
    public string Tradition { get; init; } = "";

    /// <summary>Sources of the correspondences (books, authors).</summary>
    public List<string> References { get; init; } = new();

    /// <summary>Influence ids (<see cref="Signals.InfluenceInfo.Id"/>) whose signals the rules read.</summary>
    public List<string> Requires { get; init; } = new();

    /// <summary>Reference tables of the tradition, shown in the catalog (e.g. the five-element correspondences).</summary>
    public List<ReferenceTable> Tables { get; init; } = new();

    /// <summary>Natal baseline strategy id, see <see cref="BaselineStrategies"/>.</summary>
    public string Baseline { get; init; } = BaselineStrategies.None;

    /// <summary>Symbol (sign, body, point) → body zones.</summary>
    public Dictionary<string, string[]> Correspondences { get; init; } = new();

    /// <summary>How much natal emphasis on a zone amplifies current activations (0 = none).</summary>
    public double NatalAmplification { get; init; } = 0.5;

    /// <summary>Raw activation that maps to score ≈ 0.63 (score = 1 - e^(-raw/saturation)). Higher = less saturated.</summary>
    public double Saturation { get; init; } = 2.5;

    public List<Rule> Rules { get; init; } = new();

    /// <summary>
    /// Reference distribution chosen for this evaluation (global, or the climate zone of the person's place): node
    /// scores become rarities. Null → the fallback scale 1 − e^(−raw/<see cref="Saturation"/>). Set by the API from
    /// rules/&lt;id&gt;.quantiles.json, never read from the rule file.
    /// </summary>
    [JsonIgnore] public NodeQuantiles? Rarity { get; init; }

    /// <summary>State of the school's quantile table (set by the API catalog).</summary>
    [JsonIgnore] public CalibrationState Calibration { get; init; } = CalibrationState.Missing;

    private IReadOnlySet<string>? _covers;

    /// <summary>
    /// The nodes this school can speak about: every zone its rules name or its correspondences reach. Used to combine
    /// schools by consensus: a school that cannot speak about a node takes no part in that node's average.
    /// </summary>
    public IReadOnlySet<string> Covers => _covers ??= Rules
        .SelectMany(r => r.Effect.Zones ?? [])
        .Concat(Correspondences.Values.SelectMany(z => z))
        .ToHashSet();

    public IReadOnlyList<string> ZonesOf(string symbol) =>
        Correspondences.TryGetValue(symbol, out var z) ? z : [];
}

public sealed record ReferenceTable
{
    public required string Title { get; init; }
    public List<string> Columns { get; init; } = new();
    public List<List<string>> Rows { get; init; } = new();
}

public sealed record Rule
{
    public required string Id { get; init; }
    public required RuleCondition When { get; init; }
    public required RuleEffect Effect { get; init; }
    /// <summary>Explanation template; {tag} placeholders are replaced with localized tag values.</summary>
    public string Text { get; init; } = "";
}

public sealed record RuleCondition
{
    public required string Kind { get; init; }
    /// <summary>Tag name → accepted values. All listed tags must match one of their values; "*" accepts any value (tag present).</summary>
    public Dictionary<string, string[]>? Tags { get; init; }
    public double MinIntensity { get; init; }
}

public sealed record RuleEffect
{
    /// <summary>Explicit zones.</summary>
    public string[]? Zones { get; init; }
    /// <summary>Tag names whose values (signs/bodies/points) are mapped to zones via correspondences.</summary>
    public string[]? ZonesFrom { get; init; }
    public double Weight { get; init; } = 1.0;
    /// <summary>Fixed quality, or null to read it from the signal's "quality" tag.</summary>
    public string? Quality { get; init; }
    /// <summary>Optional per-tag-value multipliers, e.g. {"body": {"moon": 1.0, "pluto": 0.3}}.</summary>
    public Dictionary<string, Dictionary<string, double>>? TagWeights { get; init; }
}

public static class RuleSetJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static RuleSet Parse(string json)
    {
        var set = JsonSerializer.Deserialize<RuleSet>(json, Options)
                  ?? throw new InvalidDataException("Empty rule set");
        Validate(set);
        return set;
    }

    /// <summary>Fails fast on unknown zones so rule files and the body map cannot drift apart.</summary>
    public static void Validate(RuleSet set)
    {
        var unknown = set.Correspondences.Values.SelectMany(z => z)
            .Concat(set.Rules.SelectMany(r => r.Effect.Zones ?? []))
            .Where(z => !BodyZones.All.Contains(z))
            .Distinct()
            .ToList();
        if (unknown.Count > 0)
            throw new InvalidDataException($"Rule set '{set.Id}' uses unknown zones: {string.Join(", ", unknown)}");

        var dup = set.Rules.GroupBy(r => r.Id).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) throw new InvalidDataException($"Rule set '{set.Id}' has duplicate rule id '{dup.Key}'");

        var badNodes = set.Rules
            .Where(r => r.When.Kind == SignalKinds.Node)
            .SelectMany(r => r.When.Tags?.GetValueOrDefault("node") ?? [])
            .Where(n => n != "*" && !BodyZones.All.Contains(n))
            .Distinct()
            .ToList();
        if (badNodes.Count > 0)
            throw new InvalidDataException($"Rule set '{set.Id}' has node rules on unknown nodes: {string.Join(", ", badNodes)}");

        if (!BaselineStrategies.All.Contains(set.Baseline))
            throw new InvalidDataException($"Rule set '{set.Id}' has unknown baseline '{set.Baseline}'");
    }

    /// <summary>
    /// Fails fast when a school requires an influence that is not registered, or a rule matches a signal kind
    /// that none of its required influences emits (it could never fire).
    /// </summary>
    public static void ValidateInfluences(RuleSet set, IReadOnlyCollection<InfluenceInfo> influences)
    {
        var byId = influences.ToDictionary(i => i.Id);
        var missing = set.Requires.Where(id => !byId.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"School '{set.Id}' requires unknown influences: {string.Join(", ", missing)}");

        var required = set.Requires.Select(id => byId[id]).ToList();
        var orphan = set.Rules
            .Where(r => r.When.Kind != SignalKinds.Node && !required.Any(i => i.Emits(r.When.Kind))).Select(r => $"{r.Id} ({r.When.Kind})").ToList();
        if (orphan.Count > 0)
            throw new InvalidDataException(
                $"School '{set.Id}' has rules on kinds no required influence emits: {string.Join(", ", orphan)}");
    }
}

public static class BaselineStrategies
{
    /// <summary>Zones of the signs and points of the birth chart (luminaries and Ascendant weigh most).</summary>
    public const string Melothesia = "melothesia";

    /// <summary>Zones of the Human Design centers defined in the natal chart.</summary>
    public const string HumanDesign = "human-design";

    /// <summary>No natal emphasis.</summary>
    public const string None = "none";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Melothesia, HumanDesign, None };
}
