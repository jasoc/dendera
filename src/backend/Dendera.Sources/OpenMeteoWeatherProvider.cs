using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Dendera.Core.Signals;
using Microsoft.Extensions.Caching.Memory;

namespace Dendera.Sources;

/// <summary>
/// Local weather from Open-Meteo (free, no key): pressure, pressure change, temperature swing, humidity.
/// Uses the forecast API for the last ~3 months and the archive API for older dates.
/// </summary>
public sealed class OpenMeteoWeatherProvider(IHttpClientFactory httpFactory, IMemoryCache cache, TimeProvider time) : ISignalProvider
{
    public const string ForecastClient = "open-meteo-forecast";
    public const string ArchiveClient = "open-meteo-archive";
    private const int SampleEveryHours = 3;

    public string Source => "open-meteo";
    public TimeSpan SampleValidity => TimeSpan.FromHours(SampleEveryHours);

    public async Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var from = DateOnly.FromDateTime(query.From.UtcDateTime).AddDays(-1); // one extra day for 24h deltas
        var to = DateOnly.FromDateTime(query.To.UtcDateTime);
        if (to > today.AddDays(14)) to = today.AddDays(14);
        if (from > to) return [];

        var hourly = new SortedDictionary<DateTimeOffset, (double? p, double? t, double? h)>();
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
        SortedDictionary<DateTimeOffset, (double? p, double? t, double? h)> into, CancellationToken ct)
    {
        var lat = Math.Round(q.Latitude, 2).ToString(CultureInfo.InvariantCulture);
        var lon = Math.Round(q.Longitude, 2).ToString(CultureInfo.InvariantCulture);
        var url = $"{path}?latitude={lat}&longitude={lon}&hourly=surface_pressure,temperature_2m,relative_humidity_2m" +
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
            into[at] = (At(h.SurfacePressure, i), At(h.Temperature, i), At(h.Humidity, i));
        }
    }

    private IReadOnlyList<Signal> ToSignals(SortedDictionary<DateTimeOffset, (double? p, double? t, double? h)> hourly, SignalQuery q)
    {
        var signals = new List<Signal>();
        foreach (var (at, now) in hourly)
        {
            if (at.Hour % SampleEveryHours != 0 || at < q.From.AddHours(-SampleEveryHours) || at > q.To) continue;
            hourly.TryGetValue(at.AddHours(-24), out var dayBefore);

            if (now.p is { } p)
            {
                signals.Add(Make("pressure", SignalKinds.Pressure, at, Math.Clamp((1015 - p) / 25, 0, 1), p, "hPa",
                    new(), "Pressione atmosferica", $"{p:0} hPa"));

                if (dayBefore.p is { } p0)
                {
                    var dp = p - p0;
                    signals.Add(Make("pressure-change", SignalKinds.PressureChange, at, Math.Clamp(Math.Abs(dp) / 12, 0, 1),
                        Math.Round(dp, 1), "hPa/24h",
                        new() { ["direction"] = dp < 0 ? "drop" : "rise" },
                        "Variazione di pressione", $"{dp:+0.0;-0.0} hPa in 24h"));
                }
            }

            if (now.t is { } t && dayBefore.t is { } t0)
            {
                var dt = t - t0;
                signals.Add(Make("temperature-swing", SignalKinds.TemperatureSwing, at, Math.Clamp(Math.Abs(dt) / 10, 0, 1),
                    Math.Round(dt, 1), "°C/24h", new() { ["direction"] = dt < 0 ? "colder" : "warmer" },
                    "Sbalzo termico", $"{t:0.0}°C ({dt:+0.0;-0.0}°C in 24h)"));
            }

            if (now.h is { } rh)
            {
                signals.Add(Make("humidity", SignalKinds.Humidity, at, Math.Clamp((rh - 60) / 40, 0, 1), rh, "%",
                    new(), "Umidità", $"{rh:0}%"));
            }
        }
        return signals;
    }

    private Signal Make(string key, string kind, DateTimeOffset at, double intensity, double value, string unit,
        Dictionary<string, string> tags, string label, string detail) =>
        new(key, kind, Source, at, intensity, value, unit, tags, label, detail);

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
    }
}
