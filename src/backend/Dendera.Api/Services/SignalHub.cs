using Dendera.Core.Signals;

namespace Dendera.Api.Services;

public sealed record SourceStatus(string Source, bool Ok, int Samples, string? Error);

public sealed record SignalBundle(
    IReadOnlyList<Signal> Sky,
    IReadOnlyList<Signal> External,
    IReadOnlyList<SourceStatus> Sources,
    IReadOnlyDictionary<string, TimeSpan> Validity)
{
    public IEnumerable<Signal> All => Sky.Concat(External);

    /// <summary>Active signals at an instant: sky samples at exactly that instant + latest valid external samples.</summary>
    public IReadOnlyList<Signal> ActiveAt(DateTimeOffset at, ILookup<DateTimeOffset, Signal> skyByTime) =>
        skyByTime[at]
            .Concat(External.GroupBy(s => s.Source)
                .SelectMany(g => SignalSet.LatestAt(g, at, Validity.GetValueOrDefault(g.Key, TimeSpan.FromHours(3)))))
            .ToList();
}

/// <summary>
/// Orchestrates the pipeline inputs: pure sky signals from the Core + every registered impure provider.
/// A failing provider never breaks a reading; its status is reported instead.
/// </summary>
public sealed class SignalHub(SkySignalGenerator sky, IEnumerable<ISignalProvider> providers, ILogger<SignalHub> log)
{
    public async Task<SignalBundle> CollectAsync(
        IReadOnlyList<DateTimeOffset> frames, SkyContext ctx, CancellationToken ct)
    {
        var from = frames[0];
        var to = frames[^1];

        var skySignals = frames.Count == 1
            ? sky.At(from, ctx)
            : sky.Range(from, to, frames.Count > 1 ? frames[1] - frames[0] : TimeSpan.FromHours(6), ctx);

        var query = new SignalQuery(from, to, ctx.Latitude, ctx.Longitude);
        var tasks = providers.Select(async p =>
        {
            try
            {
                var signals = await p.GetAsync(query, ct);
                return (p, signals, status: new SourceStatus(p.Source, true, signals.Count, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Signal source {Source} failed", p.Source);
                return (p, signals: (IReadOnlyList<Signal>)[], status: new SourceStatus(p.Source, false, 0, ex.Message));
            }
        }).ToList();
        var results = await Task.WhenAll(tasks);

        return new SignalBundle(
            skySignals,
            results.SelectMany(r => r.signals).ToList(),
            results.Select(r => r.status).Prepend(new SourceStatus(SkySignalGenerator.Source, true, skySignals.Count, null)).ToList(),
            providers.ToDictionary(p => p.Source, p => p.SampleValidity));
    }
}
