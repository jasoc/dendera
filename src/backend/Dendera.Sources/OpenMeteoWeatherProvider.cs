using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Dendera.Core;
using Dendera.Core.Signals;
using Microsoft.Extensions.Caching.Memory;

namespace Dendera.Sources;

/// <summary>
/// Local weather from Open-Meteo (free, no key): pressure, pressure change, temperature and its swing, humidity,
/// dryness, wind.
/// Uses the forecast API for the last ~3 months and the archive API for older dates.
/// </summary>
public sealed class OpenMeteoWeatherProvider(IHttpClientFactory httpFactory, IMemoryCache cache, TimeProvider time) : ISignalProvider
{
    public const string ForecastClient = "open-meteo-forecast";
    public const string ArchiveClient = "open-meteo-archive";
    private const int SampleEveryHours = 3;

    public static readonly InfluenceInfo Influence = new(
        "open-meteo",
        "Meteo locale",
        "Il tempo atmosferico nel luogo in cui vivi, ora per ora: pressione dell'aria e sue variazioni, " +
        "temperatura e sbalzi termici, umidità, vento. Dati di modelli meteorologici e rianalisi storiche.",
        "open-meteo",
        ProviderKinds.Api,
        "Open-Meteo",
        "https://open-meteo.com",
        "CC BY 4.0",
        Reliabilities.Primary,
        [
            new(SignalKinds.Pressure, "Pressione atmosferica al suolo; più intensa quando è bassa"),
            new(SignalKinds.PressureChange, "Variazione di pressione nelle ultime 24 ore (calo o salita)"),
            new(SignalKinds.TemperatureSwing, "Sbalzo di temperatura nelle ultime 24 ore"),
            new(SignalKinds.Humidity, "Umidità relativa dell'aria; più intensa quando è alta"),
            new(SignalKinds.Dryness, "Aria secca: più intensa quando l'umidità relativa scende sotto il 45%"),
            new(SignalKinds.Muggy, "Afa: caldo umido, sopra 26°C con umidità oltre il 55%"),
            new(SignalKinds.Temperature, "Temperatura dell'aria; intensa quando fa freddo (sotto 10°C) o caldo (sopra 26°C)"),
            new(SignalKinds.Wind, "Vento a 10 m e raffiche; più intenso con raffiche forti"),
        ]);

    public InfluenceInfo Info => Influence;
    public string Source => Influence.Id;
    public TimeSpan SampleValidity => TimeSpan.FromHours(SampleEveryHours);

    public async Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var from = DateOnly.FromDateTime(query.From.UtcDateTime).AddDays(-1); // one extra day for 24h deltas
        var to = DateOnly.FromDateTime(query.To.UtcDateTime);
        if (to > today.AddDays(14)) to = today.AddDays(14);
        if (from > to) return [];

        var hourly = new SortedDictionary<DateTimeOffset, Hour>();
        var forecastStart = today.AddDays(-85);

        if (from < forecastStart)
        {
            var archiveEnd = Min(to, today.AddDays(-6));
            if (archiveEnd >= from)
                await Fetch(ArchiveClient, "v1/archive", query, from, archiveEnd, hourly, cancellationToken);
        }
        if (to >= forecastStart)
            await Fetch(ForecastClient, "v1/forecast", query, Max(from, forecastStart), to, hourly, cancellationToken);

        return ToSignals(hourly, query);
    }

    private async Task Fetch(
        string client, string path, SignalQuery q, DateOnly from, DateOnly to,
        SortedDictionary<DateTimeOffset, Hour> into, CancellationToken ct)
    {
        var lat = Math.Round(q.Latitude, 2).ToString(CultureInfo.InvariantCulture);
        var lon = Math.Round(q.Longitude, 2).ToString(CultureInfo.InvariantCulture);
        var url = $"{path}?latitude={lat}&longitude={lon}&hourly=surface_pressure,temperature_2m,relative_humidity_2m,wind_speed_10m,wind_gusts_10m" +
                  $"&start_date={from:yyyy-MM-dd}&end_date={to:yyyy-MM-dd}&timezone=UTC";

        var data = await cache.GetOrCreateAsync($"open-meteo:{url}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60);
            return await httpFactory.CreateClient(client).GetFromJsonAsync<Response>(url, ct);
        });

        var h = data?.Hourly;
        if (h?.Time is null) return;
        for (var i = 0; i < h.Time.Count; i++)
        {
            var at = new DateTimeOffset(DateTime.Parse(h.Time[i], CultureInfo.InvariantCulture), TimeSpan.Zero);
            into[at] = new Hour(At(h.SurfacePressure, i), At(h.Temperature, i), At(h.Humidity, i),
                At(h.WindSpeed, i), At(h.WindGusts, i));
        }
    }

    private IReadOnlyList<Signal> ToSignals(SortedDictionary<DateTimeOffset, Hour> hourly, SignalQuery q) =>
        ToSignals(Source, hourly, q.From, q.To);

    /// <summary>Hourly values → 3-hourly signals between from and to (pure, for tests).</summary>
    public static IReadOnlyList<Signal> ToSignals(
        string source, IReadOnlyDictionary<DateTimeOffset, Hour> hourly, DateTimeOffset from, DateTimeOffset to)
    {
        var signals = new List<Signal>();
        foreach (var (at, now) in hourly)
        {
            if (at.Hour % SampleEveryHours != 0 || at < from.AddHours(-SampleEveryHours) || at > to) continue;
            var dayBefore = hourly.GetValueOrDefault(at.AddHours(-24)) ?? Hour.Empty;
            Signal Make(string key, string kind, double intensity, double value, string unit,
                Dictionary<string, string> tags, string label, string detail) =>
                new(key, kind, source, at, intensity, value, unit, tags, label, detail);

            if (now.Pressure is { } p)
            {
                signals.Add(Make("pressure", SignalKinds.Pressure, Math.Clamp((1015 - p) / 25, 0, 1), p, "hPa",
                    new(), "Pressione atmosferica", $"{p:0} hPa"));

                if (dayBefore.Pressure is { } p0)
                {
                    var dp = p - p0;
                    signals.Add(Make("pressure-change", SignalKinds.PressureChange, Math.Clamp(Math.Abs(dp) / 12, 0, 1),
                        Math.Round(dp, 1), "hPa/24h",
                        new() { ["direction"] = dp < 0 ? "drop" : "rise" },
                        "Variazione di pressione", $"{dp:+0.0;-0.0} hPa in 24h"));
                }
            }

            if (now.Temperature is { } temp)
            {
                var level = temp < 10 ? "cold" : temp > 26 ? "heat" : "mild";
                var intensity = level switch
                {
                    "cold" => Math.Clamp((10 - temp) / 15, 0, 1),
                    "heat" => Math.Clamp((temp - 26) / 12, 0, 1),
                    _ => 0,
                };
                signals.Add(Make("temperature", SignalKinds.Temperature, intensity, Math.Round(temp, 1), "°C",
                    new() { ["level"] = level }, "Temperatura", $"{temp:0.0}°C ({Lexicon.Name(level)})"));
            }

            if (now.Temperature is { } t && dayBefore.Temperature is { } t0)
            {
                var dt = t - t0;
                signals.Add(Make("temperature-swing", SignalKinds.TemperatureSwing, Math.Clamp(Math.Abs(dt) / 10, 0, 1),
                    Math.Round(dt, 1), "°C/24h", new() { ["direction"] = dt < 0 ? "colder" : "warmer" },
                    "Sbalzo termico", $"{t:0.0}°C ({dt:+0.0;-0.0}°C in 24h)"));
            }

            if (now.Humidity is { } rh)
            {
                var level = rh < 45 ? "low" : rh > 75 ? "high" : "normal";
                signals.Add(Make("humidity", SignalKinds.Humidity, Math.Clamp((rh - 60) / 40, 0, 1), rh, "%",
                    new() { ["level"] = level }, "Umidità", $"{rh:0}%"));
                if (rh < 45)
                {
                    signals.Add(Make("dryness", SignalKinds.Dryness, Math.Clamp((45 - rh) / 30, 0, 1), rh, "%",
                        new() { ["level"] = "low" }, "Aria secca", $"umidità {rh:0}%"));
                }
            }

            if (now.Temperature is { } tm && now.Humidity is { } hm && tm > 26 && hm > 55)
            {
                var k = Math.Clamp((tm - 26) / 8, 0, 1) * Math.Clamp((hm - 55) / 35, 0, 1);
                signals.Add(Make("muggy", SignalKinds.Muggy, Math.Clamp(0.2 + 0.8 * k, 0, 1), Math.Round(tm, 1), "°C",
                    new() { ["level"] = "heat" }, "Afa", $"{tm:0.0}°C con umidità {hm:0}%"));
            }

            if (now.WindSpeed is { } wind)
            {
                var gust = now.WindGusts ?? wind;
                var level = gust switch { < 20 => "calm", < 40 => "breezy", < 62 => "windy", _ => "strong" };
                signals.Add(Make("wind", SignalKinds.Wind, Math.Clamp((gust - 20) / 50, 0, 1), Math.Round(wind, 1), "km/h",
                    new() { ["level"] = level }, "Vento", $"{wind:0} km/h, raffiche {gust:0} km/h"));
            }
        }
        return signals;
    }

    /// <summary>One hour of weather (km/h for wind, °C, hPa, %).</summary>
    public sealed record Hour(double? Pressure, double? Temperature, double? Humidity, double? WindSpeed, double? WindGusts)
    {
        public static readonly Hour Empty = new(null, null, null, null, null);
    }

    private static double? At(List<double?>? list, int i) => list is not null && i < list.Count ? list[i] : null;
    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private sealed class Response
    {
        [JsonPropertyName("hourly")] public HourlyData? Hourly { get; set; }
    }

    private sealed class HourlyData
    {
        [JsonPropertyName("time")] public List<string>? Time { get; set; }
        [JsonPropertyName("surface_pressure")] public List<double?>? SurfacePressure { get; set; }
        [JsonPropertyName("temperature_2m")] public List<double?>? Temperature { get; set; }
        [JsonPropertyName("relative_humidity_2m")] public List<double?>? Humidity { get; set; }
        [JsonPropertyName("wind_speed_10m")] public List<double?>? WindSpeed { get; set; }
        [JsonPropertyName("wind_gusts_10m")] public List<double?>? WindGusts { get; set; }
    }
}
