using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dendera.Core.Signals;
using Microsoft.Extensions.Caching.Memory;

namespace Dendera.Sources;

/// <summary>
/// Schumann resonance activity index from schumannresonancelive.com (JSON, CC0, no key, ~4-minute refresh).
/// <b>Derived data</b>: a third party digitises the Tomsk State University spectrograms and partly models them into
/// a relative 0–100 score — not a physical unit. Only the last 24 hours exist: older dates yield no samples, which
/// the correlation engine counts as intensity 0 (persisting samples to build history is a follow-up).
/// </summary>
public sealed class SchumannProvider(HttpClient http, IMemoryCache cache) : ISignalProvider
{
    public const string BaseUrl = "https://schumannresonancelive.com/";

    public static readonly InfluenceInfo Influence = new(
        "schumann",
        "Risonanza di Schumann",
        "Attività delle risonanze di Schumann, le onde elettromagnetiche naturali (~7,8 Hz) tra il suolo e la ionosfera. " +
        "Indice relativo 0–100 ricavato da terzi digitalizzando gli spettrogrammi dell'Università statale di Tomsk e in " +
        "parte modellato: non è un'unità fisica. Sono disponibili solo le ultime 24 ore.",
        "schumann",
        ProviderKinds.Api,
        "schumannresonancelive.com (spettrogrammi Tomsk TSU)",
        "https://schumannresonancelive.com",
        "CC0",
        Reliabilities.Derived,
        [new(SignalKinds.Schumann, "Indice orario di attività (scala relativa Tomsk 0–100), ultime 24 ore")]);

    public InfluenceInfo Info => Influence;
    public string Source => Influence.Id;
    public TimeSpan SampleValidity => TimeSpan.FromHours(2); // hourly points

    public async Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        // One small document with the last 24 h, shared by every query (filtered below).
        var json = await cache.GetOrCreateAsync("schumann:data", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await http.GetStringAsync("api/data.php", cancellationToken);
        });
        if (json is null) return [];
        return Parse(json)
            .Where(s => s.At >= query.From - SampleValidity && s.At <= query.To)
            .ToList();
    }

    /// <summary>
    /// Maps <c>activity.series</c> (24 hourly points, the last one = the hour of <c>activity.updated</c>, UTC) to signals.
    /// </summary>
    public static IReadOnlyList<Signal> Parse(string json)
    {
        var data = JsonSerializer.Deserialize<Response>(json);
        var activity = data?.Activity;
        if (activity?.Series is not { Count: > 0 } series || activity.Updated is null) return [];

        var updated = DateTimeOffset.Parse(activity.Updated, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
        var lastHour = new DateTimeOffset(updated.Year, updated.Month, updated.Day, updated.Hour, 0, 0, TimeSpan.Zero);

        var signals = new List<Signal>(series.Count);
        for (var i = 0; i < series.Count; i++)
        {
            var point = series[i];
            var at = lastHour.AddHours(i - (series.Count - 1));
            // Sanity check: the point's own hour label must match the reconstructed hour.
            if (int.TryParse(point.T, out var h) && h != at.Hour) continue;
            if (point.S is not { } score) continue;

            var tags = new Dictionary<string, string>();
            if (i == series.Count - 1 && !string.IsNullOrEmpty(activity.Level)) tags["level"] = activity.Level;
            signals.Add(new Signal(
                "schumann", SignalKinds.Schumann, Influence.Id, at, Math.Clamp(score / 100.0, 0, 1), score, "indice",
                tags,
                "Risonanza di Schumann (indice derivato)",
                $"indice {score:0}/100 (scala relativa Tomsk, dato derivato)"));
        }
        return signals;
    }

    private sealed class Response
    {
        [JsonPropertyName("activity")] public ActivityData? Activity { get; set; }
    }

    private sealed class ActivityData
    {
        [JsonPropertyName("score")] public double? Score { get; set; }
        [JsonPropertyName("level")] public string? Level { get; set; }
        [JsonPropertyName("updated")] public string? Updated { get; set; }
        [JsonPropertyName("series")] public List<Point>? Series { get; set; }
    }

    private sealed class Point
    {
        [JsonPropertyName("t")] public string? T { get; set; }
        [JsonPropertyName("s")] public double? S { get; set; }
    }
}
