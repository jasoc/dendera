using Dendera.Core.Astro;
using Dendera.Core.Rules;
using Dendera.Core.Signals;

namespace Dendera.Core.Tests;

public class HumanDesignTests
{
    private static readonly AstronomyEngineEphemeris Eph = new();

    [Fact]
    public void Mandala_has_every_gate_once_and_known_positions()
    {
        Assert.Equal(Enumerable.Range(1, 64), HumanDesign.Mandala.Order());
        Assert.Equal((41, 1), HumanDesign.GateAt(302.0));
        Assert.Equal(25, HumanDesign.GateAt(0.0).Gate);          // 0° Aries
        Assert.Equal(25, HumanDesign.GateAt(358.25 + 0.01).Gate); // 28°15' Pisces
        Assert.Equal(17, HumanDesign.GateAt(3.875 + 0.01).Gate);  // 3°52'30" Aries
        Assert.Equal(1, HumanDesign.GateAt(223.25 + 0.01).Gate);  // 13°15' Scorpio
        Assert.Equal(2, HumanDesign.GateAt(43.25 + 0.01).Gate);   // 13°15' Taurus
        Assert.Equal(64, HumanDesign.GateAt(161.375 + 0.01).Gate); // 11°22'30" Virgo
        Assert.Equal((60, 6), HumanDesign.GateAt(301.99));
    }

    [Fact]
    public void Centers_hold_64_gates_and_channels_join_two_centers()
    {
        var all = HumanDesign.Centers.Values.SelectMany(g => g).ToList();
        Assert.Equal(64, all.Count);
        Assert.Equal(Enumerable.Range(1, 64), all.Order());
        Assert.Equal(36, HumanDesign.Channels.Count);
        Assert.Equal(36, HumanDesign.Channels.Distinct().Count());
        Assert.All(HumanDesign.Channels, c => Assert.NotEqual(HumanDesign.CenterOf(c.A), HumanDesign.CenterOf(c.B)));
    }

    [Fact]
    public void Design_is_88_degrees_of_Sun_before_birth()
    {
        var birth = new DateTimeOffset(1990, 6, 15, 10, 30, 0, TimeSpan.Zero);
        var design = HumanDesign.DesignTime(Eph, birth);
        var arc = Zodiac.Norm(Eph.Position(Bodies.Sun, birth).Longitude - Eph.Position(Bodies.Sun, design).Longitude);
        Assert.Equal(88.0, arc, 3);
        Assert.InRange((birth - design).TotalDays, 85, 95);

        var chart = HumanDesign.Compute(Eph, birth);
        Assert.Equal(13, chart.Personality.Count);
        Assert.Equal(13, chart.Design.Count);
        Assert.All(chart.DefinedChannels, c => Assert.True(chart.Gates.Contains(c.A) && chart.Gates.Contains(c.B)));
        Assert.All(chart.DefinedCenters, c => Assert.Contains(chart.DefinedChannels,
            ch => HumanDesign.CenterOf(ch.A) == c || HumanDesign.CenterOf(ch.B) == c));
    }

    [Fact]
    public async Task Transits_carry_center_natal_state_and_activate_the_school()
    {
        var natal = ChartCalculator.Compute(Eph, new BirthData(new DateTimeOffset(1990, 6, 15, 10, 30, 0, TimeSpan.Zero), 38.1, 13.4));
        var at = new DateTimeOffset(2026, 11, 20, 18, 0, 0, TimeSpan.Zero);
        var signals = await new HumanDesignProvider(Eph).GetAsync(new SignalQuery(at, at, 45, 9, natal), default);

        Assert.Equal(13, signals.Count);
        Assert.All(signals, s => Assert.NotNull(s.Tag("natalCenter")));
        var sun = signals.Single(s => s.Key == "hd-gate:sun");
        Assert.Equal(HumanDesign.GateAt(Eph.Position(Bodies.Sun, at).Longitude).Gate.ToString(), sun.Tag("gate"));

        var school = PipelineTests.Load("human-design");
        var hd = HumanDesign.Compute(Eph, natal.Birth.BirthUtc);
        var baselines = NatalBaselines.For([school], natal, () => hd.DefinedCenters);
        var zones = SchoolEngine.Evaluate([school], signals, baselines);
        Assert.Contains(zones, z => z.Score > 0);
        Assert.All(zones.SelectMany(z => z.Contributions), c => Assert.Equal("human-design", c.SchoolId));
    }
}
