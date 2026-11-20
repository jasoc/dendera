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
        var specs = new[] { new AspectSpec("square", 90, 7, "tension") };
        var hit = AspectFinder.Find(88, 1, 0, 0, specs, 1);
        Assert.NotNull(hit);
        Assert.Equal("square", hit!.Aspect.Id);
        Assert.True(hit.Applying);
        Assert.Equal(2, hit.Deviation, 6);
        Assert.Null(AspectFinder.Find(70, 1, 0, 0, specs, 1));
    }
}
