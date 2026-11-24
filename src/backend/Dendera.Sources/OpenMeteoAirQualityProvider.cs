using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Dendera.Core;
using Dendera.Core.Signals;
using Microsoft.Extensions.Caching.Memory;

namespace Dendera.Sources;

/// <summary>
/// Air quality, pollen and UV at the person's place from the Open-Meteo air quality API (CAMS, Copernicus; free, no
/// key, CC BY 4.0). Pollen only for Europe (CAMS European model). Sampled every 3 hours like the weather.
/// </summary>
public sealed class OpenMeteoAirQualityProvider(IHttpClientFactory httpFactory, IMemoryCache cache, TimeProvider time) : ISignalProvider
{
    public const string Client = "open-meteo-air";
    private const int SampleEveryHours = 3;

    public static readonly InfluenceInfo Influence = new(
        "air-quality",
        "Aria, pollini e UV",
        "Qualità dell'aria nel luogo attuale (polveri sottili PM2,5 e PM10, ozono, biossido di azoto), pollini delle " +
        "principali piante allergeniche (solo Europa) e indice UV, dal servizio europeo Copernicus (CAMS) tramite Open-Meteo.",
        "air-quality",
        ProviderKinds.Api,
        "Open-Meteo Air Quality API (Copernicus CAMS)",
        "https://open-meteo.com/en/docs/air-quality-api",
        "CC BY 4.0",
        Reliabilities.Primary,
        [
            new(SignalKinds.AirQuality, "Concentrazione di un inquinante rispetto alle linee guida OMS 2021 (PM2,5, PM10, ozono, NO₂)"),
            new(SignalKinds.Pollen, "Pollini nell'aria (granuli/m³): graminacee, betulla, olivo, ambrosia, ontano, artemisia"),
            new(SignalKinds.Uv, "Indice UV (scala OMS 0–11+)"),
        ]);

    /// <summary>WHO global air quality guidelines 2021: PM2.5 24-h 15, PM10 24-h 45, O₃ 8-h 100, NO₂ 24-h 25 µg/m³.</summary>
    public static readonly IReadOnlyDictionary<string, double> WhoGuideline = new Dictionary<string, double>
    {
        ["pm2_5"] = 15, ["pm10"] = 45, ["ozone"] = 100, ["nitrogen_dioxide"] = 25,
    };

    /// <summary>
    /// Pollen counts at which a day is "high": grass 50 and birch 81 grains/m³ (UK Met Office pollen count bands);
    /// the other species 50 by convention.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, double> PollenHigh = new Dictionary<string, double>
    {
        ["grass_pollen"] = 50, ["birch_pollen"] = 81, ["olive_pollen"] = 50, ["ragweed_pollen"] = 50,
        ["alder_pollen"] = 50, ["mugwort_pollen"] = 50,
    };

    public InfluenceInfo Info => Influence;
    public string Source => Influence.Id;
    public TimeSpan SampleValidity => TimeSpan.FromHours(SampleEveryHours);

    public async Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var from = DateOnly.FromDateTime(query.From.UtcDateTime);
        var to = DateOnly.FromDateTime(query.To.UtcDateTime);
        if (to > today.AddDays(4)) to = today.AddDays(4);
        if (from < new DateOnly(2013, 1, 1)) from = new DateOnly(2013, 1, 1);
        if (from > to) return [];

        var lat = Math.Round(query.Latitude, 2).ToString(CultureInfo.InvariantCulture);
        var lon = Math.Round(query.Longitude, 2).ToString(CultureInfo.InvariantCulture);
        var fields = string.Join(",", WhoGuideline.Keys.Concat(PollenHigh.Keys).Append("uv_index"));
        var url = $"v1/air-quality?latitude={lat}&longitude={lon}&hourly={fields}&start_date={from:yyyy-MM-dd}&end_date={to:yyyy-MM-dd}&timezone=UTC";
        var data = await cache.GetOrCreateAsync($"open-meteo-air:{url}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60);
            return await httpFactory.CreateClient(Client).GetFromJsonAsync<Response>(url, cancellationToken);
        });
        if (data?.Hourly is not { Time: { } times } h) return [];

        var signals = new List<Signal>();
        for (var i = 0; i < times.Count; i++)
        {
            var at = new DateTimeOffset(DateTime.Parse(times[i], CultureInfo.InvariantCulture), TimeSpan.Zero);
            if (at.Hour % SampleEveryHours != 0 || at < query.From - SampleValidity || at > query.To) continue;
            foreach (var (field, value) in h.Values(i))
            {
                if (value is not { } v) continue;
                if (WhoGuideline.TryGetValue(field, out var guide)) signals.Add(AirSignal(at, field, v, guide));
                else if (PollenHigh.TryGetValue(field, out var high)) { if (v > 0) signals.Add(PollenSignal(at, field, v, high)); }
                else if (field == "uv_index") signals.Add(UvSignal(at, v));
            }
        }
        return signals;
    }

    /// <summary>Intensity 0.5 at the WHO guideline, 1 at twice it.</summary>
    public static Signal AirSignal(DateTimeOffset at, string pollutant, double value, double guideline)
    {
        var level = value <= guideline ? "within" : value <= 2 * guideline ? "above" : "far-above";
        return new Signal($"air:{pollutant}", SignalKinds.AirQuality, Influence.Id, at, Math.Clamp(value / (2 * guideline), 0, 1),
            Math.Round(value, 1), "µg/m³", new Dictionary<string, string> { ["pollutant"] = pollutant, ["level"] = level },
            $"Aria: {Lexicon.Name(pollutant)}", $"{value:0.0} µg/m³ (linea guida OMS {guideline:0})");
    }

    /// <summary>Intensity 0.5 at the "high" count, 1 at twice it.</summary>
    public static Signal PollenSignal(DateTimeOffset at, string type, double value, double high) =>
        new($"pollen:{type}", SignalKinds.Pollen, Influence.Id, at, Math.Clamp(value / (2 * high), 0, 1),
            Math.Round(value), "granuli/m³", new Dictionary<string, string> { ["type"] = type },
            $"Pollini: {Lexicon.Name(type)}", $"{value:0} granuli/m³");

    /// <summary>WHO Global Solar UV Index (2002): 11+ extreme → intensity 1.</summary>
    public static Signal UvSignal(DateTimeOffset at, double uv) =>
        new("uv", SignalKinds.Uv, Influence.Id, at, Math.Clamp(uv / 11, 0, 1), Math.Round(uv, 1), "indice",
            new Dictionary<string, string> { ["level"] = uv switch { < 3 => "low", < 6 => "moderate", < 8 => "high", < 11 => "very-high", _ => "extreme" } },
            "Indice UV", $"UV {uv:0.0}");

    private sealed class Response
    {
        [JsonPropertyName("hourly")] public HourlyData? Hourly { get; set; }
    }

    private sealed class HourlyData
    {
        [JsonPropertyName("time")] public List<string>? Time { get; set; }
        [JsonPropertyName("pm2_5")] public List<double?>? Pm25 { get; set; }
        [JsonPropertyName("pm10")] public List<double?>? Pm10 { get; set; }
        [JsonPropertyName("ozone")] public List<double?>? Ozone { get; set; }
        [JsonPropertyName("nitrogen_dioxide")] public List<double?>? No2 { get; set; }
        [JsonPropertyName("uv_index")] public List<double?>? Uv { get; set; }
        [JsonPropertyName("grass_pollen")] public List<double?>? Grass { get; set; }
        [JsonPropertyName("birch_pollen")] public List<double?>? Birch { get; set; }
        [JsonPropertyName("olive_pollen")] public List<double?>? Olive { get; set; }
        [JsonPropertyName("ragweed_pollen")] public List<double?>? Ragweed { get; set; }
        [JsonPropertyName("alder_pollen")] public List<double?>? Alder { get; set; }
        [JsonPropertyName("mugwort_pollen")] public List<double?>? Mugwort { get; set; }

        public IEnumerable<(string Field, double? Value)> Values(int i)
        {
            double? At(List<double?>? l) => l is not null && i < l.Count ? l[i] : null;
            yield return ("pm2_5", At(Pm25));
            yield return ("pm10", At(Pm10));
            yield return ("ozone", At(Ozone));
            yield return ("nitrogen_dioxide", At(No2));
            yield return ("uv_index", At(Uv));
            yield return ("grass_pollen", At(Grass));
            yield return ("birch_pollen", At(Birch));
            yield return ("olive_pollen", At(Olive));
            yield return ("ragweed_pollen", At(Ragweed));
            yield return ("alder_pollen", At(Alder));
            yield return ("mugwort_pollen", At(Mugwort));
        }
    }
}
