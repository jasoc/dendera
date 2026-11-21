using Dendera.Core.Calibration;
using Dendera.Core.Rules;

namespace Dendera.Core.Tests;

public class CalibrationTests
{
    private static IEnumerable<RuleSet> Schools() =>
        Directory.GetFiles(PipelineTests.RulesDir(), "*.json")
            .Where(f => !f.EndsWith(".quantiles.json"))
            .Select(f => RuleSetJson.Parse(File.ReadAllText(f)));

    private static SchoolCalibration? TableOf(string id)
    {
        var path = Path.Combine(PipelineTests.RulesDir(), $"{id}.quantiles.json");
        return File.Exists(path) ? SchoolCalibration.Parse(File.ReadAllText(path)) : null;
    }

    /// <summary>
    /// A school changed (or an influence bumped <see cref="ReferenceSet.SignalsVersion"/>, or the reference changed)
    /// and the ritual was not run again: fails, so a stale table cannot ship by accident.
    /// Fix: `dotnet run --project Dendera.Api -- calibrate` from src/backend.
    /// </summary>
    [Fact]
    public void Every_school_has_an_up_to_date_quantile_table()
    {
        // Only the committed manifest is read: the fixtures themselves are downloaded on demand and gitignored.
        var manifestPath = Path.Combine(Path.GetDirectoryName(PipelineTests.RulesDir())!, "calibration", FixtureManifest.FileName);
        var manifest = FixtureManifest.Parse(File.ReadAllText(manifestPath));
        Assert.Equal(
            new[] { "kp.csv.gz" }.Concat(ClimateZones.All.Select(z => $"weather-{z.Id}.csv.gz")).Order(StringComparer.Ordinal),
            manifest.Files.Keys);
        var referenceHash = CalibrationHash.Reference(ReferenceSet.Default, manifest);

        foreach (var school in Schools())
        {
            var table = TableOf(school.Id);
            Assert.True(table is not null, $"{school.Id}: no quantile table, run the calibration ritual");
            Assert.True(table!.RulesHash == CalibrationHash.Rules(school), $"{school.Id}: rule file changed, run the calibration ritual");
            Assert.True(table.SignalsVersion == ReferenceSet.SignalsVersion, $"{school.Id}: signals changed, run the calibration ritual");
            Assert.True(table.ReferenceHash == referenceHash, $"{school.Id}: reference set changed, run the calibration ritual");
            var expected = ClimateZones.IsClimateDependent(school)
                ? ClimateZones.All.Select(z => z.Id).Order(StringComparer.Ordinal).ToList()
                : [SchoolCalibration.Global];
            Assert.Equal(expected, table.Tables.Keys.ToList());
        }
    }

    [Fact]
    public void Rules_hash_ignores_texts_but_not_weights()
    {
        var school = PipelineTests.Load("melothesia");
        var renamed = school with { Name = "x", Description = "y", References = ["z"] };
        Assert.Equal(CalibrationHash.Rules(school), CalibrationHash.Rules(renamed));

        var rule = school.Rules[0];
        var heavier = school with { Rules = [rule with { Effect = rule.Effect with { Weight = rule.Effect.Weight + 0.1 } }, .. school.Rules.Skip(1)] };
        Assert.NotEqual(CalibrationHash.Rules(school), CalibrationHash.Rules(heavier));
    }

    [Fact]
    public void Rarity_is_the_share_strictly_below_and_zero_stays_zero()
    {
        // 70% of the reference at zero, then 0.01 … 0.30.
        var q = Enumerable.Range(0, NodeQuantiles.Count).Select(k => k <= 70 ? 0.0 : (k - 70) / 100.0).ToArray();
        Assert.Equal(0, NodeQuantiles.RarityOf(q, 0));
        Assert.InRange(NodeQuantiles.RarityOf(q, 0.001), 0.70, 0.71);
        Assert.Equal(0.80, NodeQuantiles.RarityOf(q, 0.10), 6);
        Assert.Equal(1, NodeQuantiles.RarityOf(q, 5));
    }

    [Fact]
    public void Quantiles_are_deterministic_and_ordered()
    {
        var rng = new Random(1);
        var samples = Enumerable.Range(0, 1000).Select(_ => (float)rng.NextDouble()).ToArray();
        var a = NodeQuantiles.Of((float[])samples.Clone());
        var b = NodeQuantiles.Of((float[])samples.Clone());
        Assert.Equal(a, b);
        Assert.True(a.Zip(a.Skip(1)).All(p => p.First <= p.Second));
    }

    [Fact]
    public void Calibrated_school_scores_rarity_and_qualities_split_it()
    {
        var school = PipelineTests.Load("biometeorology");
        var table = TableOf(school.Id)!.Table("csa-palermo")!;
        var calibrated = school with { Rarity = table };
        var signals = new[]
        {
            new Signals.Signal("kp", Signals.SignalKinds.Geomagnetic, "gfz", DateTimeOffset.UnixEpoch, 0.8, 8, "Kp",
                new Dictionary<string, string> { ["level"] = "storm-g4" }, "Kp", "Kp 8"),
        };
        var zones = RuleEngine.Evaluate(calibrated, signals);
        var heart = zones.Single(z => z.Zone == "heart");
        Assert.Equal(table.Rarity("heart", heart.Raw), heart.Score);
        Assert.True(heart.Score > 0.9, $"a G4 storm should be rare on the heart, got {heart.Score:0.00}");
        Assert.Equal(heart.Score, heart.QualityScores.Values.Sum(), 6);
        Assert.Equal(0, zones.Single(z => z.Zone == "knees").Score);
    }
}
