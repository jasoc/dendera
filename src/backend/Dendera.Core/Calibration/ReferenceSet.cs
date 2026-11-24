using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dendera.Core.Astro;
using Dendera.Core.Rules;

namespace Dendera.Core.Calibration;

/// <summary>
/// The synthetic reference "sky of always" every school is calibrated against. No personal data: births are
/// generated (seeded), instants are random over a long span so the slow cycles are covered.
/// </summary>
/// <param name="Charts">Synthetic births, spread over <see cref="BirthFromYear"/>–<see cref="BirthToYear"/>, all hours,
/// latitudes 0–<see cref="MaxLatitude"/>° north and south.</param>
/// <param name="Instants">Random instants over <see cref="FromYear"/>–<see cref="ToYear"/> for schools that read only
/// the sky (the slow cycles need the long span).</param>
/// <param name="ClimateSamples">Random 3-hour slots per climate zone over the weather fixture span, for schools that
/// read the local weather.</param>
public sealed record ReferenceSet(
    int Seed = 20261120,
    int Charts = 500,
    int Instants = 50_000,
    int FromYear = 1900,
    int ToYear = 2100,
    int BirthFromYear = 1930,
    int BirthToYear = 2010,
    double MaxLatitude = 60,
    int ClimateSamples = 20_000,
    int DataFromYear = 1991,
    int DataToYear = 2020)
{
    /// <summary>
    /// Bump whenever an influence changes how it computes intensity or which signals it emits (orbs, Kp mapping,
    /// weather thresholds, new kinds): every quantile table becomes stale and the ritual must run again.
    /// </summary>
    public const int SignalsVersion = 4; // 2: decans, stars, ganzhi, hours, lunar stations, life times; 3: dignity, solar hour, sidereal; 4: space weather, air, decumbiture

    public static readonly ReferenceSet Default = new();

    /// <summary>The synthetic births (deterministic).</summary>
    public IReadOnlyList<BirthData> Births()
    {
        var rng = new Random(Seed);
        var from = new DateTimeOffset(BirthFromYear, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var span = (new DateTimeOffset(BirthToYear + 1, 1, 1, 0, 0, 0, TimeSpan.Zero) - from).TotalSeconds;
        return Enumerable.Range(0, Charts).Select(_ =>
        {
            var at = from.AddSeconds(Math.Floor(rng.NextDouble() * span));
            // Uniform on the sphere between the two parallels, so high latitudes are not over-represented.
            var s = Math.Sin(MaxLatitude * Math.PI / 180);
            var lat = Math.Asin((rng.NextDouble() * 2 - 1) * s) * 180 / Math.PI;
            var lon = rng.NextDouble() * 360 - 180;
            return new BirthData(at, Math.Round(lat, 4), Math.Round(lon, 4));
        }).ToList();
    }

    /// <summary>Random instants over the long span (deterministic, seed + 1), whole minutes.</summary>
    public IReadOnlyList<DateTimeOffset> SkyInstants()
    {
        var rng = new Random(Seed + 1);
        var from = new DateTimeOffset(FromYear, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var minutes = (new DateTimeOffset(ToYear + 1, 1, 1, 0, 0, 0, TimeSpan.Zero) - from).TotalMinutes;
        return Enumerable.Range(0, Instants).Select(_ => from.AddMinutes(Math.Floor(rng.NextDouble() * minutes))).ToList();
    }

    /// <summary>Random 3-hour slots inside the weather span, per climate zone (deterministic, seed + 2 + zone index).
    /// The first two days are skipped: the weather signals look 48 hours back.</summary>
    public IReadOnlyList<DateTimeOffset> ClimateInstants(int zoneIndex)
    {
        var rng = new Random(Seed + 2 + zoneIndex);
        var from = new DateTimeOffset(DataFromYear, 1, 3, 0, 0, 0, TimeSpan.Zero);
        var slots = (int)((new DateTimeOffset(DataToYear + 1, 1, 1, 0, 0, 0, TimeSpan.Zero) - from).TotalHours / 3);
        return Enumerable.Range(0, ClimateSamples).Select(_ => from.AddHours(3 * rng.Next(slots))).ToList();
    }
}

/// <summary>Hashes that tie a quantile table to the rule file and to the reference set it was computed from.</summary>
public static class CalibrationHash
{
    /// <summary>
    /// Hash of what changes a school's raw sums: requires, baseline, correspondences, natal amplification, saturation
    /// (it drives the second pass) and the rules' conditions and effects. Names, texts, descriptions, references,
    /// tables and icons are left out: editing them does not need a new ritual.
    /// </summary>
    public static string Rules(RuleSet s)
    {
        var projection = new
        {
            s.Id,
            s.Requires,
            s.Baseline,
            Correspondences = s.Correspondences.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => new { kv.Key, kv.Value }),
            s.NatalAmplification,
            s.Saturation,
            Rules = s.Rules.Select(r => new { r.Id, r.When, r.Effect }),
        };
        return Sha(JsonSerializer.Serialize(projection));
    }

    /// <summary>
    /// Hash of the reference: its parameters, the signals version and the content hashes of the fixtures as recorded
    /// in the committed manifest (the fixtures themselves are downloaded, not committed).
    /// </summary>
    public static string Reference(ReferenceSet set, FixtureManifest manifest)
    {
        var text = new StringBuilder(JsonSerializer.Serialize(set)).Append($"|signals:{ReferenceSet.SignalsVersion}");
        foreach (var (name, hash) in manifest.Files) text.Append($"|{name}:{hash}");
        return Sha(text.ToString());
    }

    /// <summary>SHA-256 of a fixture's decompressed content (independent of how gzip packed it).</summary>
    public static string Content(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
