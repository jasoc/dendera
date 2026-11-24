using Dendera.Core.Rules;
using Dendera.Core.Signals;

namespace Dendera.Core.Tests;

public class LineageTests
{
    private static RuleSet School(string id, string lineage, string parents, double weight) => RuleSetJson.Parse($$"""
        { "id": "{{id}}", "version": "1", "lineage": "{{lineage}}", "parents": [{{parents}}], "name": "{{id}}", "rules": [
          { "id": "r", "when": { "kind": "wind" }, "effect": { "zones": ["heart"], "weight": {{weight}} } } ] }
        """);

    private static readonly Signal Wind = new("wind", SignalKinds.Wind, "test", DateTimeOffset.UnixEpoch, 1, null, null,
        new Dictionary<string, string>(), "wind", "");

    [Fact]
    public void One_vote_per_lineage_keeps_a_family_from_outvoting_an_independent_tradition()
    {
        // Two schools of one family see the heart, an independent one does not touch it (weight 0 → score 0).
        var a = School("a", "western-astrology", "", 5);
        var b = School("b", "hermetic", "\"western-astrology\"", 5);
        var c = School("c", "chinese-classics", "", 0);
        Assert.False(Lineages.Independent(a, b));
        Assert.True(Lineages.Independent(a, c));

        var schools = new[] { a, b, c };
        var plain = SchoolEngine.Evaluate(schools, [Wind]).Single(z => z.Zone == "heart").Score;
        var byLineage = SchoolEngine.Evaluate(schools, [Wind], mode: ConsensusMode.Lineage).Single(z => z.Zone == "heart").Score;
        var one = SchoolEngine.Evaluate([a], [Wind]).Single(z => z.Zone == "heart").Score;

        Assert.Equal(2 * one / 3, plain, 6);      // three votes, two of them the same family
        Assert.Equal(one / 2, byLineage, 6);      // two families: astrology (a, b) and Chinese medicine (c)
    }

    [Fact]
    public void A_school_borrowing_from_many_does_not_merge_every_family()
    {
        var astrology = School("astro", "western-astrology", "", 1);
        var chinese = School("tcm", "chinese-classics", "", 1);
        var hd = School("hd", "human-design", "\"western-astrology\", \"chinese-classics\"", 1);
        var families = SchoolEngine.Families([astrology, chinese, hd]);
        Assert.Equal(["western-astrology", "chinese-classics", "western-astrology"], families);
    }
}
