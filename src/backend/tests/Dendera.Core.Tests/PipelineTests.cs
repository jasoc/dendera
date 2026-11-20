using Dendera.Core.Astro;
using Dendera.Core.Journal;
using Dendera.Core.Readings;
using Dendera.Core.Rules;
using Dendera.Core.Signals;

namespace Dendera.Core.Tests;

public class PipelineTests
{
    private static RuleSet LoadClassic()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "rules"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return RuleSetJson.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "rules", "melothesia-classic.json")));
    }

    private static Signal Sig(string key, string kind, double intensity, Dictionary<string, string>? tags = null) =>
        new(key, kind, "test", DateTimeOffset.UnixEpoch, intensity, null, null, tags ?? new(), key, "");

    [Fact]
    public void Classic_rule_set_loads_and_validates()
    {
        var rules = LoadClassic();
        Assert.Equal("melothesia-classic", rules.Id);
        Assert.Contains("gut", rules.ZonesOf("virgo"));
        Assert.True(rules.Rules.Count > 10);
    }

    [Fact]
    public void Unknown_zone_is_rejected()
    {
        const string json = """
        { "id": "x", "version": "1", "name": "x", "correspondences": { "aries": ["elbow"] } }
        """;
        Assert.Throws<InvalidDataException>(() => RuleSetJson.Parse(json));
    }

    [Fact]
    public void Geomagnetic_storm_activates_nervous_system()
    {
        var zones = RuleEngine.Evaluate(LoadClassic(), [Sig("kp", SignalKinds.Geomagnetic, 0.8)]);
        var nervous = zones.Single(z => z.Zone == "nervous-system");
        Assert.Equal(1 - Math.Exp(-0.8 / 2.5), nervous.Score, 6);
        Assert.Equal("agitation", nervous.DominantQuality);
        Assert.Equal(0, zones.Single(z => z.Zone == "feet").Score);
    }

    [Fact]
    public void Transit_maps_zones_through_correspondences_and_quality_tag()
    {
        var signal = Sig("transit:saturn:square:sun", SignalKinds.TransitAspect, 1.0, new()
        {
            ["transitBody"] = "saturn", ["natalPoint"] = "sun", ["natalSign"] = "virgo",
            ["aspect"] = "square", ["quality"] = "tension",
        });
        var zones = RuleEngine.Evaluate(LoadClassic(), [signal]).Where(z => z.Score > 0).Select(z => z.Zone).ToHashSet();
        Assert.Contains("skeleton", zones); // Saturn
        Assert.Contains("heart", zones);    // natal Sun
        Assert.Contains("gut", zones);      // Virgo
        var c = RuleEngine.Evaluate(LoadClassic(), [signal]).Single(z => z.Zone == "gut").Contributions[0];
        Assert.Equal("tension", c.Quality);
        Assert.Contains("Saturno", c.Text);
    }

    [Fact]
    public void Natal_baseline_amplifies_activation()
    {
        var rules = LoadClassic();
        var signal = Sig("kp", SignalKinds.Geomagnetic, 0.5);
        var plain = RuleEngine.Evaluate(rules, [signal]).Single(z => z.Zone == "heart").Raw;
        var amplified = RuleEngine.Evaluate(rules, [signal], new Dictionary<string, double> { ["heart"] = 1 })
            .Single(z => z.Zone == "heart").Raw;
        Assert.Equal(plain * 1.5, amplified, 6);
    }

    [Fact]
    public void Sky_generator_produces_expected_kinds_for_a_natal_chart()
    {
        var eph = new AstronomyEngineEphemeris();
        var rules = LoadClassic();
        var natal = ChartCalculator.Compute(eph, new BirthData(new DateTimeOffset(1990, 6, 15, 10, 30, 0, TimeSpan.Zero), 38.1, 13.4));
        var gen = new SkySignalGenerator(eph);
        var ctx = new SkyContext(45, 9, natal, rules.Aspects, rules.Orbs.Transit, rules.Orbs.Sky);

        var signals = gen.At(new DateTimeOffset(2024, 4, 10, 0, 0, 0, TimeSpan.Zero), ctx);
        var kinds = signals.Select(s => s.Kind).ToHashSet();
        Assert.Contains(SignalKinds.Placement, kinds);
        Assert.Contains(SignalKinds.Retrograde, kinds);
        Assert.Contains(SignalKinds.TransitAspect, kinds);
        Assert.Contains(SignalKinds.LunarPhase, kinds);
        Assert.Contains(SignalKinds.Photoperiod, kinds);
        Assert.Contains(SignalKinds.Eclipse, kinds); // 8 April 2024 is two days earlier
        Assert.Contains(signals, s => s.Key == "retrograde:mercury");
        Assert.All(signals, s => Assert.InRange(s.Intensity, 0, 1));

        var reading = ReadingEngine.Compose(DateTimeOffset.UtcNow, rules, eph.Positions(DateTimeOffset.UtcNow), natal, signals);
        Assert.Equal(BodyZones.All.Count, reading.Zones.Count);
    }

    [Fact]
    public void Correlation_finds_a_planted_effect_and_ignores_noise()
    {
        var rng = new Random(1);
        var start = new DateOnly(2026, 1, 1);
        var days = Enumerable.Range(0, 60).Select(i => start.AddDays(i)).ToList();

        var storm = days.ToDictionary(d => d, _ => rng.NextDouble() < 0.3 ? 0.8 + rng.NextDouble() * 0.2 : 0.0);
        var noise = days.ToDictionary(d => d, _ => rng.NextDouble());

        var journal = days.Select(d => new JournalEntry(d, new Dictionary<string, int>
        {
            ["sleep"] = storm[d] > 0 ? rng.Next(1, 3) : rng.Next(3, 6), // storms wreck sleep
            ["mood"] = rng.Next(1, 6),                                  // unrelated
        }, null)).ToList();

        var findings = CorrelationEngine.Analyze(journal,
        [
            new DailySeries("kp", SignalKinds.Geomagnetic, "Kp", storm),
            new DailySeries("noise", "noise", "Noise", noise),
        ]);

        var sleepStorm = findings.Single(f => f.Metric == "sleep" && f.SignalKey == "kp");
        Assert.Equal("strong", sleepStorm.Evidence);
        Assert.True(sleepStorm.Pearson < -0.5);
        Assert.True(sleepStorm.MeanWhenActive < sleepStorm.MeanWhenQuiet);
        Assert.DoesNotContain(findings, f => f.Metric == "mood" && f.Evidence == "strong");
    }

    [Fact]
    public void Benjamini_Hochberg_matches_reference()
    {
        var q = CorrelationEngine.BenjaminiHochberg([0.01, 0.04, 0.03, 0.2]);
        Assert.Equal(0.04, q[0], 6);
        Assert.Equal(0.0533333, q[1], 5);
        Assert.Equal(0.0533333, q[2], 5);
        Assert.Equal(0.2, q[3], 6);
    }
}
