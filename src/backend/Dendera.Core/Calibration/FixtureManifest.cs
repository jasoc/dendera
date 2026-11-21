using System.Text;
using System.Text.Json;

namespace Dendera.Core.Calibration;

/// <summary>
/// The committed record of the reference fixtures (src/backend/calibration/manifest.json): for every file, the
/// SHA-256 of its decompressed content. The fixtures are downloaded on demand (`fetch-reference`) and gitignored; the
/// ritual refuses files that do not match the manifest, so a table always comes from the data the manifest names.
/// </summary>
public sealed record FixtureManifest
{
    public const string FileName = "manifest.json";

    /// <summary>File name → SHA-256 of the decompressed content.</summary>
    public required SortedDictionary<string, string> Files { get; init; }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static FixtureManifest Parse(string json) =>
        JsonSerializer.Deserialize<FixtureManifest>(json, Json) ?? throw new InvalidDataException("Empty fixture manifest");

    /// <summary>Deterministic text (sorted keys, LF).</summary>
    public string ToJson()
    {
        var sb = new StringBuilder("{\n  \"files\": {\n");
        var i = 0;
        foreach (var (name, hash) in Files)
            sb.Append($"    \"{name}\": \"{hash}\"").Append(++i < Files.Count ? ",\n" : "\n");
        return sb.Append("  }\n}\n").ToString();
    }
}
