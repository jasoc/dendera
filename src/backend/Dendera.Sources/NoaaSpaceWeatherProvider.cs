using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Dendera.Core;
using Dendera.Core.Signals;
using Microsoft.Extensions.Caching.Memory;

namespace Dendera.Sources;

/// <summary>
/// Space weather from NOAA SWPC (JSON, public domain, no key): the real-time solar wind at L1 (speed and the
/// north–south component Bz of the interplanetary magnetic field, last 24 hours) and the GOES X-ray flux (flares,
/// last 7 days). Hourly samples; the short history is kept by the API's sample store.
/// </summary>
public sealed class NoaaSpaceWeatherProvider(HttpClient http, IMemoryCache cache, TimeProvider time) : ISignalProvider
{
    public const string BaseUrl = "https://services.swpc.noaa.gov/";

    public static readonly InfluenceInfo Influence = new(
        "noaa-swpc",
        "Meteo spaziale (NOAA)",
        "Il Sole visto dai satelliti: la velocità del vento solare e la componente Bz del campo magnetico " +
        "interplanetario misurate al punto L1 (ultime 24 ore), e il flusso di raggi X dei satelliti GOES che registra i " +
        "brillamenti (ultimi 7 giorni). Sono le cause delle tempeste geomagnetiche che il Kp misura a terra.",
        "noaa-swpc",
        ProviderKinds.Api,
        "NOAA Space Weather Prediction Center",
        "https://www.swpc.noaa.gov",
        "Pubblico dominio (governo USA)",
        Reliabilities.Primary,
        [
            new(SignalKinds.SolarWind, "Velocità del vento solare (km/s), media oraria; più intensa sopra i 400 km/s"),
            new(SignalKinds.ImfBz, "Componente nord–sud del campo magnetico interplanetario (nT); più intensa quando punta a sud"),
            new(SignalKinds.XrayFlare, "Flusso massimo orario di raggi X (0,1–0,8 nm, GOES): brillamenti di classe C, M, X"),
        ]);

    public InfluenceInfo Info => Influence;
    public string Source => Influence.Id;
    public TimeSpan SampleValidity => TimeSpan.FromHours(2);

    public async Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        var signals = new List<Signal>();
        var since = query.From - SampleValidity;
        if (query.To < time.GetUtcNow().AddDays(-8)) return signals; // nothing this old in the feeds

        var wind = await Fetch<List<WindRow>>("json/rtsw/rtsw_wind_1m.json", cancellationToken) ?? [];
        var mag = await Fetch<List<MagRow>>("json/rtsw/rtsw_mag_1m.json", cancellationToken) ?? [];
        var xray = await Fetch<List<XrayRow>>("json/goes/primary/xrays-7-day.json", cancellationToken) ?? [];

        foreach (var g in wind.Where(r => r.Active && r.Speed is > 0).GroupBy(r => Hour(r.Time)))
            signals.Add(SolarWindSignal(g.Key, g.Average(r => r.Speed!.Value)));
        foreach (var g in mag.Where(r => r.Active && r.Bz is not null).GroupBy(r => Hour(r.Time)))
            signals.Add(BzSignal(g.Key, g.Min(r => r.Bz!.Value)));
        foreach (var g in xray.Where(r => r.Energy == "0.1-0.8nm" && r.Flux is > 0).GroupBy(r => Hour(r.Time)))
            signals.Add(XraySignal(g.Key, g.Max(r => r.Flux!.Value)));

        return signals.Where(s => s.At >= since && s.At <= query.To).ToList();
    }

    private Task<T?> Fetch<T>(string path, CancellationToken ct) =>
        cache.GetOrCreateAsync($"noaa:{path}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await http.GetFromJsonAsync<T>(path, ct);
        });

    private static DateTimeOffset Hour(string time)
    {
        var t = DateTimeOffset.Parse(time.EndsWith('Z') ? time : time + "Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        return new DateTimeOffset(t.Year, t.Month, t.Day, t.Hour, 0, 0, TimeSpan.Zero);
    }

    /// <summary>
    /// Slow wind ~400 km/s, high-speed streams 500–800 (NOAA SWPC, "Solar wind" glossary): intensity 0 at 400, 1 at 800.
    /// </summary>
    public static Signal SolarWindSignal(DateTimeOffset at, double speed)
    {
        var level = speed switch { < 500 => "slow", < 700 => "fast", _ => "very-fast" };
        return new Signal("solar-wind", SignalKinds.SolarWind, Influence.Id, at, Math.Clamp((speed - 400) / 400, 0, 1),
            Math.Round(speed), "km/s", new Dictionary<string, string> { ["level"] = level },
            "Vento solare", $"{speed:0} km/s ({Lexicon.Name(level)})");
    }

    /// <summary>
    /// Southward Bz drives storms: −10 nT for three hours or more marks an intense storm (Gonzalez et al., "What is a
    /// geomagnetic storm?", J. Geophys. Res. 99, 1994). Intensity 0 at 0 nT, 1 at −10 nT; northward = 0.
    /// </summary>
    public static Signal BzSignal(DateTimeOffset at, double bz) =>
        new("imf-bz", SignalKinds.ImfBz, Influence.Id, at, Math.Clamp(-bz / 10, 0, 1), Math.Round(bz, 1), "nT",
            new Dictionary<string, string> { ["direction"] = bz < 0 ? "south" : "north" },
            "Campo magnetico interplanetario (Bz)", $"Bz {bz:+0.0;-0.0} nT ({(bz < 0 ? "verso sud" : "verso nord")})");

    /// <summary>
    /// Flare classes by peak 0.1–0.8 nm flux: C 10⁻⁶, M 10⁻⁵, X 10⁻⁴ W/m²; NOAA radio blackout scale R1 = M1, R3 = X1.
    /// Intensity 0 at C1, 0.5 at M1, 1 at X1 (logarithmic).
    /// </summary>
    public static Signal XraySignal(DateTimeOffset at, double flux)
    {
        var exponent = Math.Log10(flux);
        var cls = exponent switch { < -7 => "A", < -6 => "B", < -5 => "C", < -4 => "M", _ => "X" };
        var magnitude = flux / Math.Pow(10, Math.Floor(exponent));
        return new Signal("xray", SignalKinds.XrayFlare, Influence.Id, at, Math.Clamp((exponent + 6) / 2, 0, 1),
            flux, "W/m²", new Dictionary<string, string> { ["class"] = cls },
            "Raggi X dal Sole (GOES)", $"classe {cls}{magnitude:0.0}");
    }

    private sealed class WindRow
    {
        [JsonPropertyName("time_tag")] public string Time { get; set; } = "";
        [JsonPropertyName("active")] public bool Active { get; set; }
        [JsonPropertyName("proton_speed")] public double? Speed { get; set; }
    }

    private sealed class MagRow
    {
        [JsonPropertyName("time_tag")] public string Time { get; set; } = "";
        [JsonPropertyName("active")] public bool Active { get; set; }
        [JsonPropertyName("bz_gsm")] public double? Bz { get; set; }
    }

    private sealed class XrayRow
    {
        [JsonPropertyName("time_tag")] public string Time { get; set; } = "";
        [JsonPropertyName("energy")] public string Energy { get; set; } = "";
        [JsonPropertyName("flux")] public double? Flux { get; set; }
    }
}
