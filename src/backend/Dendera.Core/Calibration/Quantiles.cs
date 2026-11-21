using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dendera.Core.Calibration;

/// <summary>
/// Reference distribution of one school's raw sums, per body node: 101 quantiles (0th … 100th percentile).
/// Turns a raw sum into its <b>rarity</b>: the share of reference moments with a strictly lower raw sum.
/// </summary>
public sealed class NodeQuantiles(IReadOnlyDictionary<string, double[]> nodes)
{
    public const int Count = 101;

    public IReadOnlyDictionary<string, double[]> Nodes { get; } = nodes;

    /// <summary>
    /// Rarity 0..1 of <paramref name="raw"/> on <paramref name="node"/>, or null when the table has no such node.
    /// Zero stays zero: a node nothing touches is not "as usual", it is untouched.
    /// </summary>
    public double? Rarity(string node, double raw) =>
        Nodes.TryGetValue(node, out var q) ? RarityOf(q, raw) : null;

    /// <summary>Share of the reference strictly below <paramref name="raw"/>, interpolated between quantiles.</summary>
    public static double RarityOf(IReadOnlyList<double> q, double raw)
    {
        if (raw <= 0 || raw <= q[0]) return 0;
        if (raw > q[^1]) return 1;
        // Largest k with q[k] < raw; then q[k] < raw <= q[k + 1].
        int lo = 0, hi = q.Count - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (q[mid] < raw) lo = mid; else hi = mid;
        }
        var frac = (raw - q[lo]) / (q[hi] - q[lo]);
        return (lo + frac) / (q.Count - 1);
    }

    /// <summary>101 quantiles of the samples (linear interpolation between order statistics). Sorts in place.</summary>
    public static double[] Of(float[] samples)
    {
        Array.Sort(samples);
        var q = new double[Count];
        if (samples.Length == 0) return q;
        for (var k = 0; k < Count; k++)
        {
            var pos = k / 100.0 * (samples.Length - 1);
            var i = (int)Math.Floor(pos);
            var j = Math.Min(samples.Length - 1, i + 1);
            q[k] = samples[i] + (pos - i) * (samples[j] - samples[i]);
        }
        return q;
    }
}

/// <summary>
/// One school's quantile tables as written by the ritual (rules/&lt;school&gt;.quantiles.json): one table for the
/// whole reference sky ("global") or one per climate zone for schools that read the local weather.
/// </summary>
public sealed record SchoolCalibration
{
    public const string Global = "global";

    public required string School { get; init; }
    public required string SchoolVersion { get; init; }
    /// <summary>Hash of everything in the school that changes raw sums (<see cref="CalibrationHash.Rules"/>).</summary>
    public required string RulesHash { get; init; }
    /// <summary>Hash of the reference set: parameters, fixtures, <see cref="ReferenceSet.SignalsVersion"/>.</summary>
    public required string ReferenceHash { get; init; }
    public required int SignalsVersion { get; init; }
    public required int Samples { get; init; }
    /// <summary>Table id (<see cref="Global"/> or a climate zone id) → node → 101 quantiles of the raw sum.</summary>
    public required SortedDictionary<string, SortedDictionary<string, double[]>> Tables { get; init; }

    public NodeQuantiles? Table(string id) =>
        Tables.TryGetValue(id, out var t) ? new NodeQuantiles(t.ToDictionary(kv => kv.Key, kv => kv.Value)) : null;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static SchoolCalibration Parse(string json) =>
        JsonSerializer.Deserialize<SchoolCalibration>(json, Json) ?? throw new InvalidDataException("Empty calibration");

    /// <summary>Deterministic text: fixed key order, numbers rounded to 6 decimals, invariant culture, LF.</summary>
    public string ToJson()
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append($"  \"school\": \"{School}\",\n");
        sb.Append($"  \"schoolVersion\": \"{SchoolVersion}\",\n");
        sb.Append($"  \"rulesHash\": \"{RulesHash}\",\n");
        sb.Append($"  \"referenceHash\": \"{ReferenceHash}\",\n");
        sb.Append($"  \"signalsVersion\": {SignalsVersion},\n");
        sb.Append($"  \"samples\": {Samples},\n");
        sb.Append("  \"tables\": {\n");
        var t = 0;
        foreach (var (id, nodes) in Tables)
        {
            sb.Append($"    \"{id}\": {{\n");
            var n = 0;
            foreach (var (node, q) in nodes)
            {
                sb.Append($"      \"{node}\": [");
                sb.Append(string.Join(",", q.Select(v => Math.Round(v, 6).ToString("0.######", CultureInfo.InvariantCulture))));
                sb.Append(++n < nodes.Count ? "],\n" : "]\n");
            }
            sb.Append(++t < Tables.Count ? "    },\n" : "    }\n");
        }
        sb.Append("  }\n}\n");
        return sb.ToString();
    }
}

/// <summary>Whether a school's quantile table matches its rule file.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CalibrationState>))]
public enum CalibrationState
{
    /// <summary>Table present and up to date: scores are rarities.</summary>
    Calibrated,
    /// <summary>Rule file or signals changed after the ritual: fallback scale until it is re-run.</summary>
    Stale,
    /// <summary>No table: fallback scale.</summary>
    Missing,
}
