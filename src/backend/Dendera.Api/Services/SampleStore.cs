using System.Text.Json;
using Dendera.Api.Data;
using Dendera.Core.Signals;
using Microsoft.EntityFrameworkCore;

namespace Dendera.Api.Services;

/// <summary>
/// Wraps a source whose feed forgets quickly (Schumann, NOAA solar wind: 24 hours): every sample it returns is stored
/// in SQLite, and every query is answered from the store too, so the history grows for as long as the app runs.
/// If the live feed fails, the stored samples still answer.
/// </summary>
public sealed class PersistentProvider(ISignalProvider inner, IServiceScopeFactory scopes, ILogger<PersistentProvider> log)
    : ISignalProvider
{
    public InfluenceInfo Info => inner.Info;
    public string Source => inner.Source;
    public TimeSpan SampleValidity => inner.SampleValidity;

    public async Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<Signal> live = [];
        Exception? failure = null;
        try
        {
            live = await inner.GetAsync(query, cancellationToken);
            await StoreAsync(live, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failure = ex;
            log.LogWarning(ex, "Live feed {Source} failed; answering from the sample store", Source);
        }

        var stored = await LoadAsync(query.From - SampleValidity, query.To, cancellationToken);
        if (failure is not null && stored.Count == 0) throw failure;
        var seen = live.Select(s => (s.Key, s.At)).ToHashSet();
        return live.Concat(stored.Where(s => !seen.Contains((s.Key, s.At)))).OrderBy(s => s.At).ToList();
    }

    /// <summary>Stores new samples (existing ones with the same source, key and instant are left as they are).</summary>
    public async Task StoreAsync(IReadOnlyList<Signal> signals, CancellationToken ct)
    {
        if (signals.Count == 0) return;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DenderaDb>();
        var from = signals.Min(s => s.At).ToUnixTimeSeconds();
        var to = signals.Max(s => s.At).ToUnixTimeSeconds();
        var existing = (await db.Samples.Where(s => s.Source == Source && s.At >= from && s.At <= to)
                .Select(s => new { s.Key, s.At }).ToListAsync(ct))
            .Select(x => (x.Key, x.At)).ToHashSet();
        foreach (var s in signals)
        {
            var at = s.At.ToUnixTimeSeconds();
            if (!existing.Add((s.Key, at))) continue;
            db.Samples.Add(new StoredSample
            {
                Source = Source, Key = s.Key, Kind = s.Kind, At = at, Intensity = s.Intensity, Value = s.Value,
                Unit = s.Unit, TagsJson = JsonSerializer.Serialize(s.Tags), Label = s.Label, Detail = s.Detail,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<Signal>> LoadAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DenderaDb>();
        var f = from.ToUnixTimeSeconds();
        var t = to.ToUnixTimeSeconds();
        var rows = await db.Samples.Where(s => s.Source == Source && s.At >= f && s.At <= t).ToListAsync(ct);
        return rows.Select(r => new Signal(
            r.Key, r.Kind, r.Source, DateTimeOffset.FromUnixTimeSeconds(r.At), r.Intensity, r.Value, r.Unit,
            JsonSerializer.Deserialize<Dictionary<string, string>>(r.TagsJson) ?? new Dictionary<string, string>(),
            r.Label, r.Detail)).ToList();
    }
}

/// <summary>
/// Asks every persistent source for its last day once an hour, so its history keeps growing even when nobody opens
/// the app (the feeds keep only 24 hours).
/// </summary>
public sealed class SampleCollector(IServiceProvider services, TimeProvider clock, ILogger<SampleCollector> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = clock.GetUtcNow();
            foreach (var p in services.GetServices<ISignalProvider>().OfType<PersistentProvider>())
            {
                try
                {
                    await p.GetAsync(new SignalQuery(now.AddHours(-24), now, 0, 0), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogWarning(ex, "Collecting {Source} failed", p.Source);
                }
            }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
