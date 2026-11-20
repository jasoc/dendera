using Dendera.Core.Rules;
using Dendera.Core.Signals;

namespace Dendera.Core.Tests;

public class BodyModelTests
{
    private static Signal Sig(string key, string kind, double intensity, Dictionary<string, string>? tags = null) =>
        new(key, kind, "test", DateTimeOffset.UnixEpoch, intensity, null, null, tags ?? new(), key, "");

    [Fact]
    public void Every_node_has_a_layer_a_name_and_a_valid_seat()
    {
        Assert.Equal(BodyZones.Ordered.Count, BodyZones.All.Count);
        foreach (var n in BodyZones.Nodes)
        {
            Assert.Contains(n.Layer, BodyLayers.All);
            Assert.NotEqual(n.Id, Lexicon.Name(n.Id));
            Assert.False(string.IsNullOrWhiteSpace(n.Description));
            if (n.Seat is not null) Assert.Equal(BodyLayers.Soma, BodyZones.LayerOf(n.Seat));
        }
    }

    [Fact]
    public void Tcm_turns_an_activated_liver_into_anger_in_the_second_pass()
    {
        var tcm = PipelineTests.Load("tcm");
        var wind = Sig("wind", SignalKinds.Wind, 0.9);
        var zones = RuleEngine.Evaluate(tcm, [wind]);

        var liver = zones.Single(z => z.Zone == "zf-liver");
        var anger = zones.Single(z => z.Zone == "anger");
        Assert.True(liver.Score > 0.2);
        Assert.True(anger.Score > 0);
        var c = Assert.Single(anger.Contributions);
        Assert.Equal(("emotion-anger", "node:zf-liver"), (c.RuleId, c.SignalKey));
        // the liver also opens into the eyes, nourishes the sinews, houses the Hun and stores Blood
        foreach (var node in new[] { "eyes", "sinews", "hun", "xue" })
            Assert.True(zones.Single(z => z.Zone == node).Score > 0, node);
        Assert.Equal(liver.DominantQuality, c.Quality); // the organ's tone travels with it
        Assert.Contains("Collera", c.Text);
        Assert.Equal(BodyLayers.Emotions, anger.Layer);
    }

    [Fact]
    public void Node_rules_run_once_and_only_inside_their_own_school()
    {
        // b's liver is activated, but only a has the liver → anger relation: schools stay independent.
        var a = RuleSetJson.Parse("""
        { "id": "a", "version": "1", "name": "a", "rules": [
          { "id": "link", "when": { "kind": "node", "tags": { "node": ["liver"] } }, "effect": { "zones": ["anger"] } },
          { "id": "chain", "when": { "kind": "node", "tags": { "node": ["anger"] } }, "effect": { "zones": ["fear"] } } ] }
        """);
        var b = RuleSetJson.Parse("""
        { "id": "b", "version": "1", "name": "b", "rules": [
          { "id": "r", "when": { "kind": "wind" }, "effect": { "zones": ["liver"] } } ] }
        """);
        var zones = SchoolEngine.Evaluate([a, b], [Sig("wind", SignalKinds.Wind, 1)]);
        Assert.Equal(0, zones.Single(z => z.Zone == "anger").Score);

        // Inside one school: liver → anger fires, anger → fear does not (one pass, no chains).
        var both = RuleSetJson.Parse("""
        { "id": "c", "version": "1", "name": "c", "rules": [
          { "id": "r", "when": { "kind": "wind" }, "effect": { "zones": ["liver"] } },
          { "id": "link", "when": { "kind": "node", "tags": { "node": ["liver"] } }, "effect": { "zones": ["anger"] } },
          { "id": "chain", "when": { "kind": "node", "tags": { "node": ["anger"] } }, "effect": { "zones": ["fear"] } } ] }
        """);
        var own = RuleEngine.Evaluate(both, [Sig("wind", SignalKinds.Wind, 1)]);
        Assert.True(own.Single(z => z.Zone == "anger").Score > 0);
        Assert.Equal(0, own.Single(z => z.Zone == "fear").Score);
    }

    [Fact]
    public void Node_rules_on_unknown_nodes_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => RuleSetJson.Parse("""
        { "id": "x", "version": "1", "name": "x", "rules": [
          { "id": "r", "when": { "kind": "node", "tags": { "node": ["spleen-of-doom"] } }, "effect": { "zones": ["anger"] } } ] }
        """));
    }

    [Fact]
    public void Tantric_yoga_opens_sushumna_at_twilight_and_reads_planets_in_chakras()
    {
        var yoga = PipelineTests.Load("tantric-yoga");
        var dusk = Sig("day-phase", SignalKinds.DayPhase, 1, new() { ["phase"] = "dusk", ["light"] = "twilight" });
        var saturn = Sig("transit:saturn:square:sun", SignalKinds.TransitAspect, 1, new()
        {
            ["transitBody"] = "saturn", ["natalPoint"] = "sun", ["aspect"] = "square", ["quality"] = "tension",
        });
        var zones = RuleEngine.Evaluate(yoga, [dusk, saturn]).ToDictionary(z => z.Zone);
        Assert.True(zones["sushumna"].Score > 0);
        Assert.True(zones["sahasrara"].Score > 0); // second pass: the open channel reaches the crown
        Assert.True(zones["muladhara"].Score > 0); // Saturn → root chakra
        Assert.True(zones["pelvis"].Score > 0);    // root chakra → its region
        Assert.Equal(0, zones["pingala"].Score);
    }

    [Fact]
    public void Sun_altitude_tells_day_from_night()
    {
        var eph = new Dendera.Core.Astro.AstronomyEngineEphemeris();
        var gen = new SkySignalGenerator(eph);
        var noon = new DateTimeOffset(2026, 6, 21, 11, 0, 0, TimeSpan.Zero);    // ~13:00 in Catania
        var midnight = new DateTimeOffset(2026, 6, 21, 23, 0, 0, TimeSpan.Zero);
        double Alt(DateTimeOffset t) => gen.SunAltitude(t, eph.Position("sun", t).Longitude, 37.5, 15.1, out _);
        Assert.InRange(Alt(noon), 70, 77);      // summer solstice at 37.5°N: 90 − 37.5 + 23.4 ≈ 76
        Assert.True(Alt(midnight) < -20);
    }

    [Fact]
    public void Melothesia_reads_the_humors_of_the_planets()
    {
        var mel = PipelineTests.Load("melothesia");
        var transit = Sig("transit:saturn:square:moon", SignalKinds.TransitAspect, 1, new()
        {
            ["transitBody"] = "saturn", ["natalPoint"] = "moon", ["natalSign"] = "cancer", ["aspect"] = "square", ["quality"] = "tension",
        });
        var zones = RuleEngine.Evaluate(mel, [transit]).Where(z => z.Score > 0).Select(z => z.Zone).ToHashSet();
        Assert.Contains("melancholic", zones); // Saturn
        Assert.Contains("phlegmatic", zones);  // natal Moon
        Assert.Contains("imagination", zones); // the Moon rules the brain and imagination
    }
}
