using Dendera.Core.Astro;
using Dendera.Core.Rules;
using Dendera.Core.Signals;
using Dendera.Sources;

namespace Dendera.Core.Tests;

public class TcmTests
{
    [Theory]
    [InlineData(315.0, "lichun", "spring", "wood")]
    [InlineData(0.0, "chunfen", "spring", "wood")]
    [InlineData(30.0, "guyu", "late-summer", "earth")]
    [InlineData(90.0, "xiazhi", "summer", "fire")]
    [InlineData(120.0, "dashu", "late-summer", "earth")]
    [InlineData(180.0, "qiufen", "autumn", "metal")]
    [InlineData(240.0, "xiaoxue", "winter", "water")]
    [InlineData(270.0, "dongzhi", "winter", "water")]
    [InlineData(300.0, "dahan", "late-summer", "earth")]
    public void Solar_terms_and_seasons(double sun, string term, string season, string element)
    {
        var s = TcmCalendar.SeasonOf(sun);
        Assert.Equal((term, season, element), (s.Term, s.SeasonId, s.Element));
    }

    [Theory]
    [InlineData(3, 30, 0.0, "lung")]
    [InlineData(2, 30, 15.0, "lung")]      // 02:30 UTC at 15°E = 03:30 local solar
    [InlineData(10, 59, 0.0, "spleen")]
    [InlineData(11, 0, 0.0, "heart")]
    [InlineData(0, 0, 0.0, "gallbladder")]
    [InlineData(1, 0, 0.0, "liver")]
    [InlineData(23, 0, 0.0, "gallbladder")]
    public void Organ_clock(int hour, int minute, double longitude, string organ) =>
        Assert.Equal(organ, TcmCalendar.OrganAt(new DateTimeOffset(2026, 11, 20, hour, minute, 0, TimeSpan.Zero), longitude));

    [Fact]
    public async Task Provider_emits_the_season_on_every_frame()
    {
        var provider = new TcmCalendarProvider(new AstronomyEngineEphemeris());
        var from = new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);
        var signals = await provider.GetAsync(new SignalQuery(from, from.AddDays(1), 37.5, 15, null, TimeSpan.FromHours(6)), default);
        var seasons = signals.Where(s => s.Kind == SignalKinds.SolarTerm).ToList();
        Assert.Equal(5, seasons.Count);
        Assert.All(seasons, s => Assert.Equal("water", s.Tag("element"))); // Sun ~238° on 20 Nov: Xiaoxue, winter
        Assert.Equal(5, signals.Count(s => s.Kind == SignalKinds.OrganClock));
    }

    [Fact]
    public void Weather_samples_trigger_the_six_excesses()
    {
        var tcm = PipelineTests.Load("tcm");
        var t0 = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var hourly = new SortedDictionary<DateTimeOffset, OpenMeteoWeatherProvider.Hour>
        {
            [t0.AddHours(-24)] = new(1012, 5, 30, 10, 20),
            [t0] = new(1010, 0, 30, 40, 75), // cold, dry, windy
        };
        var signals = OpenMeteoWeatherProvider.ToSignals("open-meteo", hourly, t0, t0);

        var zones = RuleEngine.Evaluate(tcm, signals);
        var fired = zones.SelectMany(z => z.Contributions).Select(c => c.RuleId).ToHashSet();
        Assert.Contains("excess-wind", fired);
        Assert.Contains("excess-cold", fired);
        Assert.Contains("excess-dryness", fired);
        Assert.DoesNotContain("excess-heat", fired);
        Assert.Contains(zones.Single(z => z.Zone == "zf-liver").Contributions, c => c.Text.Contains("Fegato"));
    }
}

public class SchumannTests
{
    [Fact]
    public void Parses_the_24_hourly_points_ending_at_the_update_hour()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "schumann.json"));
        var signals = SchumannProvider.Parse(json);

        Assert.Equal(24, signals.Count);
        Assert.All(signals, s => Assert.Equal((SignalKinds.Schumann, "schumann", "schumann"), (s.Kind, s.Key, s.Source)));
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 19, 0, 0, TimeSpan.Zero), signals[^1].At);
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 20, 0, 0, TimeSpan.Zero), signals[0].At);
        Assert.Equal(0.52, signals[^1].Intensity, 6);
        Assert.Equal("elevated", signals[^1].Tag("level"));
        Assert.Equal(54, signals[0].Value);
        Assert.Equal(Reliabilities.Derived, SchumannProvider.Influence.Reliability);
    }

    [Fact]
    public void High_schumann_index_activates_biometeorology()
    {
        var bio = PipelineTests.Load("biometeorology");
        var s = new Signal("schumann", SignalKinds.Schumann, "schumann", DateTimeOffset.UnixEpoch, 0.7, 70, "indice",
            new Dictionary<string, string>(), "Schumann", "indice 70/100");
        Assert.True(RuleEngine.Evaluate(bio, [s]).Single(z => z.Zone == "sleep").Score > 0);
    }
}
