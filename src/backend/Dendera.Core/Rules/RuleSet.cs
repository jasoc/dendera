using System.Text.Json;
using System.Text.Json.Serialization;
using Dendera.Core.Astro;

namespace Dendera.Core.Rules;

/// <summary>
/// A "school" / theory: correspondences, aspects and rules. Loaded from rules/*.json.
/// Rules are hypotheses; the journal + correlation engine tests them.
/// </summary>
public sealed record RuleSet
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";

    /// <summary>Symbol (sign, body, point) → body zones.</summary>
    public Dictionary<string, string[]> Correspondences { get; init; } = new();

    public List<AspectSpec> Aspects { get; init; } = new();

    public OrbFactors Orbs { get; init; } = new();

    /// <summary>How much natal emphasis on a zone amplifies current activations (0 = none).</summary>
    public double NatalAmplification { get; init; } = 0.5;

    /// <summary>Raw activation that maps to score ≈ 0.63 (score = 1 - e^(-raw/saturation)). Higher = less saturated.</summary>
    public double Saturation { get; init; } = 2.5;

    public List<Rule> Rules { get; init; } = new();

    public IReadOnlyList<string> ZonesOf(string symbol) =>
        Correspondences.TryGetValue(symbol, out var z) ? z : [];
}

public sealed record OrbFactors
{
    public double Transit { get; init; } = 0.4;
    public double Sky { get; init; } = 0.5;
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
    /// <summary>Tag name → accepted values. All listed tags must match one of their values.</summary>
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
    }
}
