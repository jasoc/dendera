using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Dendera.Core.Signals;
using Microsoft.Extensions.Caching.Memory;

namespace Dendera.Sources;

/// <summary>
/// Planetary Kp index (geomagnetic activity) from GFZ Potsdam: 3-hour values, history and nowcast,
/// free, no key, CC BY 4.0. https://kp.gfz.de
/// </summary>
public sealed class GfzKpProvider(HttpClient http, IMemoryCache cache) : ISignalProvider
{
    public const string BaseUrl = "https://kp.gfz.de/";
    public string Source => "gfz";
    public TimeSpan SampleValidity => TimeSpan.FromHours(6); // GFZ nowcast lags a few hours

    public async Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        // Kp is global: location does not matter. Cache per whole-day window.
        var from = query.From.UtcDateTime.Date.AddDays(-1);
        var to = query.To.UtcDateTime.Date.AddDays(1);
        var key = $"gfz-kp:{from:yyyyMMdd}:{to:yyyyMMdd}";

        var data = await cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            var url = $"app/json/?start={from:yyyy-MM-ddTHH:mm:ssZ}&end={to:yyyy-MM-ddTHH:mm:ssZ}&index=Kp";
            return await http.GetFromJsonAsync<KpResponse>(url, cancellationToken);
        });

        if (data?.Kp is null || data.Datetime is null) return [];

        var signals = new List<Signal>(data.Kp.Count);
        for (var i = 0; i < Math.Min(data.Kp.Count, data.Datetime.Count); i++)
        {
            var at = DateTimeOffset.Parse(data.Datetime[i], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            if (at < query.From - SampleValidity || at > query.To) continue;
            var kp = data.Kp[i];
            var status = data.Status is not null && i < data.Status.Count ? data.Status[i] : "";
            signals.Add(new Signal(
                "kp", SignalKinds.Geomagnetic, Source, at,
                Math.Clamp((kp - 2) / 6.0, 0, 1), kp, "Kp",
                new Dictionary<string, string> { ["level"] = Level(kp), ["status"] = status },
                "Attività geomagnetica (Kp)",
                $"Kp {kp:0.0} – {LevelLabel(kp)}"));
        }
        return signals;
    }

    private static string Level(double kp) => kp switch
    {
        < 3 => "quiet",
        < 4 => "unsettled",
        < 5 => "active",
        < 6 => "storm-g1",
        < 7 => "storm-g2",
        < 8 => "storm-g3",
        < 9 => "storm-g4",
        _ => "storm-g5",
    };

    private static string LevelLabel(double kp) => kp switch
    {
        < 3 => "quieto",
        < 4 => "instabile",
        < 5 => "attivo",
        _ => $"tempesta G{Math.Min(5, (int)kp - 4)}",
    };

    private sealed class KpResponse
    {
        [JsonPropertyName("datetime")] public List<string>? Datetime { get; set; }
        [JsonPropertyName("Kp")] public List<double>? Kp { get; set; }
        [JsonPropertyName("status")] public List<string>? Status { get; set; }
    }
}
