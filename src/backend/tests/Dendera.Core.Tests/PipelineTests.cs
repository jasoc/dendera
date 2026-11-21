using Dendera.Core.Astro;
using Dendera.Core.Journal;
using Dendera.Core.Readings;
using Dendera.Core.Rules;
using Dendera.Core.Signals;

namespace Dendera.Core.Tests;

public class PipelineTests
{
    internal static string RulesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "rules"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "rules");
    }

    internal static RuleSet Load(string id) => RuleSetJson.Parse(File.ReadAllText(Path.Combine(RulesDir(), $"{id}.json")));

    private static RuleSet LoadClassic() => Load("melothesia");

    private static RuleSet LoadBio() => Load("biometeorology");

    private static Signal Sig(string key, string kind, double intensity, Dictionary<string, string>? tags = null) =>
        new(key, kind, "test", DateTimeOffset.UnixEpoch, intensity, null, null, tags ?? new(), key, "");

    [Fact]
    public void Classic_rule_set_loads_and_validates()
    {
        var rules = LoadClassic();
        Assert.Equal("melothesia", rules.Id);
        Assert.Equal(BaselineStrategies.Melothesia, rules.Baseline);
        Assert.Contains("gut", rules.ZonesOf("virgo"));
        Assert.True(rules.Rules.Count > 10);
    }

    [Fact]
    public void Every_school_file_validates_against_the_registered_influences()
    {
        var files = Directory.GetFiles(RulesDir(), "*.json").Where(f => !f.EndsWith(".quantiles.json")).ToArray();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var school = RuleSetJson.Parse(File.ReadAllText(file));
            Assert.Equal(Path.GetFileNameWithoutExtension(file), school.Id);
            Assert.NotEmpty(school.Icon);
            Assert.NotEmpty(school.Requires);
            RuleSetJson.ValidateInfluences(school, Influences.All);
        }
    }

    [Fact]
    public void Rule_on_a_kind_no_required_influence_emits_is_rejected()
    {
        var school = RuleSetJson.Parse("""
        { "id": "x", "version": "1", "name": "x", "requires": ["gfz"],
          "rules": [ { "id": "r", "when": { "kind": "humidity" }, "effect": { "zones": ["head"] } } ] }
        """);
        Assert.Throws<InvalidDataException>(() => RuleSetJson.ValidateInfluences(school, Influences.All));
        Assert.Throws<InvalidDataException>(() =>
            RuleSetJson.ValidateInfluences(school with { Requires = ["nope"] }, Influences.All));
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
        var zones = RuleEngine.Evaluate(LoadBio(), [Sig("kp", SignalKinds.Geomagnetic, 0.8)]);
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
        var signal = Sig("eclipse:solar", SignalKinds.Eclipse, 0.5);
        var plain = RuleEngine.Evaluate(rules, [signal]).Single(z => z.Zone == "mood").Raw;
        var amplified = RuleEngine.Evaluate(rules, [signal], new Dictionary<string, double> { ["mood"] = 1 })
            .Single(z => z.Zone == "mood").Raw;
        Assert.Equal(plain * 1.5, amplified, 6);
    }

    [Fact]
    public void Sky_generator_produces_expected_kinds_for_a_natal_chart()
    {
        var eph = new AstronomyEngineEphemeris();
        var rules = LoadClassic();
        var natal = ChartCalculator.Compute(eph, new BirthData(new DateTimeOffset(1990, 6, 15, 10, 30, 0, TimeSpan.Zero), 38.1, 13.4));
        var gen = new SkySignalGenerator(eph);
        var ctx = new SkyContext(45, 9, natal);

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

        var reading = ReadingEngine.Compose(DateTimeOffset.UtcNow, [rules], null, eph.Positions(DateTimeOffset.UtcNow), natal, signals);
        Assert.Equal(BodyZones.All.Count, reading.Zones.Count);
    }

    [Fact]
    public async Task Ephemeris_provider_path_equals_the_direct_sky_generator()
    {
        var eph = new AstronomyEngineEphemeris();
        var rules = LoadClassic();
        var natal = ChartCalculator.Compute(eph, new BirthData(new DateTimeOffset(1990, 6, 15, 10, 30, 0, TimeSpan.Zero), 38.1, 13.4));
        var gen = new SkySignalGenerator(eph);
        var ctx = new SkyContext(45, 9, natal);
        var provider = new EphemerisProvider(eph);

        // single reading
        var at = new DateTimeOffset(2026, 11, 20, 18, 0, 0, TimeSpan.Zero);
        var one = await provider.GetAsync(new SignalQuery(at, at, 45, 9, natal), CancellationToken.None);
        Assert.Equal(Fingerprint(gen.At(at, ctx)), Fingerprint(one));

        // timeline: every frame's active set equals the generator at that frame, and so do the zones
        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var query = new SignalQuery(from, from.AddDays(5), 45, 9, natal, TimeSpan.FromHours(6));
        var samples = await provider.GetAsync(query, CancellationToken.None);
        var activeAt = SignalSet.ActiveAt(samples, new Dictionary<string, TimeSpan> { [provider.Source] = provider.SampleValidity });
        var baseline = RuleEngine.NatalBaseline(rules, natal);
        foreach (var frame in query.Frames())
        {
            var direct = gen.At(frame, ctx);
            Assert.Equal(Fingerprint(direct), Fingerprint(activeAt(frame)));
            Assert.Equal(
                RuleEngine.Evaluate(rules, direct, baseline).Select(z => z.Score),
                RuleEngine.Evaluate(rules, activeAt(frame), baseline).Select(z => z.Score));
        }
    }

    private static List<string> Fingerprint(IEnumerable<Signal> signals) =>
        signals.Select(s => $"{s.Key}|{s.At:O}|{s.Intensity:R}").Order().ToList();

    [Fact]
    public void Schools_combine_by_consensus_of_the_schools_that_cover_the_node()
    {
        var a = Rules("a", """{ "id": "r", "when": { "kind": "geomagnetic" }, "effect": { "zones": ["heart"], "weight": 1, "quality": "agitation" } }""");
        var b = Rules("b", """{ "id": "r", "when": { "kind": "geomagnetic" }, "effect": { "zones": ["heart", "head"], "weight": 2, "quality": "pressure" } }""");
        var signals = new List<Signal> { Sig("kp", SignalKinds.Geomagnetic, 0.5) };

        var sa = RuleEngine.Evaluate(a, signals).Single(z => z.Zone == "heart").Score;
        var sb = RuleEngine.Evaluate(b, signals).Single(z => z.Zone == "heart").Score;
        var heart = SchoolEngine.Evaluate([a, b], signals).Single(z => z.Zone == "heart");

        Assert.Equal((sa + sb) / 2, heart.Score, 12); // both schools speak about the heart
        Assert.Equal(sa, heart.BySchool["a"], 12);
        Assert.Equal(sb, heart.BySchool["b"], 12);
        Assert.Equal(["b", "a"], heart.Contributions.Select(c => c.SchoolId));
        Assert.Equal("pressure", heart.DominantQuality);
        var qa = RuleEngine.Evaluate(a, signals).Single(z => z.Zone == "heart").QualityScores["agitation"];
        Assert.Equal(1 - Math.Exp(-0.5 / 2.5), qa, 12);
        Assert.Equal(qa / 2, heart.QualityScores["agitation"], 12); // b speaks about the heart but says no agitation
        Assert.Equal(sb / 2, heart.QualityScores["pressure"], 12);

        // only b knows the head: a takes no part in that average
        var head = SchoolEngine.Evaluate([a, b], signals).Single(z => z.Zone == "head");
        Assert.Equal(RuleEngine.Evaluate(b, signals).Single(z => z.Zone == "head").Score, head.Score, 12);
        Assert.Equal(["b"], head.BySchool.Keys);
    }

    [Fact]
    public void Single_school_equals_rule_engine()
    {
        var rules = LoadBio() with { NatalAmplification = 0.5 };
        var signals = new List<Signal> { Sig("kp", SignalKinds.Geomagnetic, 0.7), Sig("ph", SignalKinds.Photoperiod, 0.4) };
        var baseline = new Dictionary<string, double> { ["sleep"] = 1 };
        var direct = RuleEngine.Evaluate(rules, signals, baseline);
        var viaSchools = SchoolEngine.Evaluate([rules], signals, new NatalBaselines { [rules.Id] = baseline });
        Assert.Equal(direct.Select(z => (z.Zone, z.Score, z.Raw)), viaSchools.Select(z => (z.Zone, z.Score, z.Raw)));
        Assert.All(viaSchools.SelectMany(z => z.Contributions), c => Assert.Equal(rules.Id, c.SchoolId));
    }

    [Fact]
    public void Timeline_has_per_school_zone_tracks_only_with_several_schools()
    {
        var a = Rules("a", """{ "id": "r", "when": { "kind": "geomagnetic" }, "effect": { "zones": ["heart"] } }""");
        var b = Rules("b", """{ "id": "r", "when": { "kind": "geomagnetic" }, "effect": { "zones": ["head"] } }""");
        var t0 = DateTimeOffset.UnixEpoch;
        var samples = new List<Signal> { Sig("kp", SignalKinds.Geomagnetic, 0.5) };
        IReadOnlyList<Signal> Active(DateTimeOffset _) => samples;

        var one = ReadingEngine.BuildTimeline(t0, t0, [a], null, samples, [t0], Active);
        Assert.Equal(BodyZones.All.Count, one.Zones.Count);

        var two = ReadingEngine.BuildTimeline(t0, t0, [a, b], null, samples, [t0], Active);
        Assert.Equal(3 * BodyZones.All.Count, two.Zones.Count);
        Assert.Contains(two.Zones, t => t.Key == "zone:a:heart" && t.Peak > 0 && t.Tags["school"] == "a");
        Assert.Contains(two.Zones, t => t.Key == "zone:b:heart" && t.Peak == 0);
        Assert.Contains(two.Zones, t => t.Key == "zone:heart" && t.Peak > 0);
    }

    private static RuleSet Rules(string id, string rule) =>
        RuleSetJson.Parse($$"""{ "id": "{{id}}", "version": "1", "name": "{{id}}", "rules": [ {{rule}} ] }""");

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
