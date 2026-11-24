using Dendera.Core.Astro;
using Dendera.Core.Signals;

namespace Dendera.Core.Tests;

public class DecumbitureTests
{
    private static readonly AstronomyEngineEphemeris Eph = new();

    [Fact]
    public void Counts_hippocratic_days_and_finds_the_moon_square_about_a_week_later()
    {
        var onset = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
        var provider = new DecumbitureProvider(Eph);

        var day7 = provider.At(onset.AddDays(6.5), [onset]);
        var count = day7.Single(s => s.Kind == SignalKinds.IllnessDay);
        Assert.Equal("7", count.Tag("day"));
        Assert.Equal("true", count.Tag("critical"));

        // The Moon moves ~13°/day: a square to its onset place falls within days 6–8, and it is a crisis.
        var crisis = Enumerable.Range(0, 72)
            .SelectMany(h => provider.At(onset.AddDays(5.5).AddHours(h), [onset]))
            .Where(s => s.Kind == SignalKinds.DecumbitureMoon && s.Tag("moonAspect") == "square")
            .ToList();
        Assert.NotEmpty(crisis);
        Assert.All(crisis, s => Assert.Equal("crisis", s.Tag("phase")));

        Assert.Empty(provider.At(onset.AddDays(31), [onset])); // followed for 30 days only
        Assert.Empty(provider.At(onset.AddHours(-1), [onset]));
    }

    [Fact]
    public void Space_weather_and_air_intensities_follow_their_scales()
    {
        var at = DateTimeOffset.UnixEpoch;
        Assert.Equal(0.5, Sources.NoaaSpaceWeatherProvider.XraySignal(at, 1e-5).Intensity, 6); // M1
        Assert.Equal("X", Sources.NoaaSpaceWeatherProvider.XraySignal(at, 2e-4).Tag("class"));
        Assert.Equal(1, Sources.NoaaSpaceWeatherProvider.BzSignal(at, -12).Intensity);
        Assert.Equal(0, Sources.NoaaSpaceWeatherProvider.BzSignal(at, 5).Intensity);
        Assert.Equal(0.5, Sources.OpenMeteoAirQualityProvider.AirSignal(at, "pm2_5", 15, 15).Intensity, 6);
        Assert.Equal(1, Sources.OpenMeteoAirQualityProvider.UvSignal(at, 12).Intensity);
    }
}
