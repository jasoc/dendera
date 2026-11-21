using Dendera.Core.Astro;
using Dendera.Core.Signals;

namespace Dendera.Core.Tests;

public class AstroTests
{
    private static readonly AstronomyEngineEphemeris Eph = new();

    private static DateTimeOffset Utc(int y, int m, int d, int h = 0, int min = 0) => new(y, m, d, h, min, 0, TimeSpan.Zero);

    [Fact]
    public void Sun_at_J2000_is_in_early_Capricorn()
    {
        var sun = Eph.Position(Bodies.Sun, Utc(2000, 1, 1, 12));
        Assert.InRange(sun.Longitude, 280.2, 280.6);
        Assert.Equal("capricorn", sun.Sign);
    }

    [Fact]
    public void Mercury_retrograde_April_2024_is_detected()
    {
        Assert.True(Eph.Position(Bodies.Mercury, Utc(2024, 4, 10)).Retrograde);
        Assert.False(Eph.Position(Bodies.Mercury, Utc(2024, 3, 15)).Retrograde);
        Assert.False(Eph.Position(Bodies.Mercury, Utc(2024, 5, 10)).Retrograde);
    }

    [Fact]
    public void Full_moon_25_January_2024()
    {
        var phase = Eph.MoonPhase(Utc(2024, 1, 25, 17, 54));
        Assert.InRange(phase, 179, 181);
        Assert.Equal("full", SkySignalGenerator.PhaseName(phase));
    }

    [Fact]
    public void Total_solar_eclipse_8_April_2024_is_found()
    {
        var eclipses = Eph.Eclipses(Utc(2024, 3, 1), Utc(2024, 5, 1));
        Assert.Contains(eclipses, e => e.Type == "solar" && e.Kind == "total" && e.Peak.Date == new DateTime(2024, 4, 8));
        Assert.Contains(eclipses, e => e.Type == "lunar" && e.Peak.Date == new DateTime(2024, 3, 25));
    }

    [Fact]
    public void Angles_at_equator_with_ramc_zero()
    {
        Assert.Equal(0, Houses.Midheaven(0, 23.44), 6);
        Assert.Equal(90, Houses.Ascendant(0, 23.44, 0), 6);
    }

    [Theory]
    [InlineData(HouseSystem.Placidus, 45.0)]
    [InlineData(HouseSystem.Placidus, -33.9)]
    [InlineData(HouseSystem.Porphyry, 45.0)]
    [InlineData(HouseSystem.WholeSign, 45.0)]
    public void House_cusps_go_around_the_zodiac_in_order(HouseSystem system, double latitude)
    {
        for (var ramc = 0.0; ramc < 360; ramc += 17)
        {
            var cusps = Houses.Cusps(system, ramc, 23.44, latitude, out _);
            var total = 0.0;
            for (var i = 0; i < 12; i++)
            {
                var span = Zodiac.Norm(cusps[(i + 1) % 12] - cusps[i]);
                Assert.InRange(span, 0.5, 90);
                total += span;
            }
            Assert.Equal(360, total, 6);
            if (system != HouseSystem.WholeSign)
            {
                Assert.Equal(Houses.Midheaven(ramc, 23.44), cusps[9], 6);
                Assert.Equal(Houses.Ascendant(ramc, 23.44, latitude), cusps[0], 6);
            }
        }
    }

    [Fact]
    public void Placidus_falls_back_to_Porphyry_in_polar_regions()
    {
        Houses.Cusps(HouseSystem.Placidus, 100, 23.44, 70, out var used);
        Assert.Equal(HouseSystem.Porphyry, used);
    }

    [Fact]
    public void Day_length_is_plausible()
    {
        Assert.InRange(SkySignalGenerator.DayLength(0, 0), 12.0, 12.2);
        Assert.InRange(SkySignalGenerator.DayLength(90, 45), 15.3, 15.7);
        Assert.InRange(SkySignalGenerator.DayLength(270, 45), 8.6, 9.0);
        Assert.Equal(24, SkySignalGenerator.DayLength(90, 75));
    }

    [Fact]
    public void Natal_chart_has_all_points_and_houses()
    {
        var chart = ChartCalculator.Compute(Eph, new BirthData(Utc(1990, 6, 15, 10, 30), 38.1157, 13.3615));
        Assert.Equal(Bodies.All.Count + 2, chart.Points.Count);
        Assert.All(chart.Points, p => Assert.InRange(p.House, 1, 12));
        Assert.Equal(1, chart.Point(Bodies.Ascendant)!.House);
        Assert.Equal(10, chart.Point(Bodies.Midheaven)!.House);
    }

    [Fact]
    public void Aspect_finder_detects_square_and_applying()
    {
        var specs = new[] { new AspectSpec("square", 90, "tension") };
        var hit = AspectFinder.Find(88, 1, 0, 0, specs, 7);
        Assert.NotNull(hit);
        Assert.Equal("square", hit!.Aspect.Id);
        Assert.True(hit.Applying);
        Assert.Equal(2, hit.Deviation, 6);
        Assert.Null(AspectFinder.Find(70, 1, 0, 0, specs, 7));
    }

    [Fact]
    public void Orbs_are_per_planet_moieties_after_al_biruni()
    {
        Assert.Equal(13.5, AspectCatalog.SkyOrb(Bodies.Sun, Bodies.Moon), 6);
        Assert.Equal(7, AspectCatalog.SkyOrb(Bodies.Mercury, Bodies.Venus), 6);
        Assert.Equal(4.5, AspectCatalog.TransitOrb(Bodies.Saturn), 6);
        Assert.Equal(2.5, AspectCatalog.TransitOrb(Bodies.Pluto), 6);

        var specs = AspectCatalog.Default;
        // Saturn 4° from an exact square to a natal point: inside its 4½° moiety, outside the old 7° × 0.4.
        Assert.Equal("square", AspectFinder.Find(94, 0.03, 0, 0, specs, AspectCatalog.TransitOrb(Bodies.Saturn))!.Aspect.Id);
        // Minor aspects get half the orb: semisquare 3° off is outside Saturn's 2¼°.
        Assert.Null(AspectFinder.Find(48, 0.03, 0, 0, specs.Where(s => !s.Major).ToList(), AspectCatalog.TransitOrb(Bodies.Saturn)));
    }

    [Fact]
    public void Retrograde_and_slow_motion_follow_lillys_ratio()
    {
        var gen = new SkySignalGenerator(Eph);
        var signals = gen.At(Utc(2026, 11, 20, 12, 0), new SkyContext(45, 9, null));
        Assert.All(signals.Where(s => s.Kind == SignalKinds.Retrograde), s => Assert.Equal(1.0, s.Intensity));
        Assert.All(signals.Where(s => s.Kind == SignalKinds.SlowMotion), s => Assert.Equal(0.4, s.Intensity));
        var moving = signals.Where(s => s.Kind is SignalKinds.Retrograde or SignalKinds.SlowMotion).Select(s => s.Tag("body")).ToList();
        Assert.Equal(moving.Count, moving.Distinct().Count());
    }
}
