using Dendera.Core.Astro;
using Dendera.Core.Signals;

namespace Dendera.Api.Services;

public sealed record SourceStatus(string Source, bool Ok, int Samples, string? Error);

public sealed record SignalBundle(
    IReadOnlyList<Signal> All,
    IReadOnlyList<SourceStatus> Sources,
    IReadOnlyDictionary<string, TimeSpan> Validity)
{
    /// <summary>Active signals at an instant: per influence, the latest valid samples (frame-aligned ones exactly at it).</summary>
    public Func<DateTimeOffset, IReadOnlyList<Signal>> ActiveAt() => SignalSet.ActiveAt(All, Validity);
}

/// <summary>
/// Orchestrates the pipeline inputs: every registered influence (pure or impure) goes through the same loop.
/// A failing influence never breaks a reading; its status is reported instead.
/// </summary>
public sealed class SignalHub(IEnumerable<ISignalProvider> providers, InfluenceStatusBoard board, ILogger<SignalHub> log)
{
    public IReadOnlyList<ISignalProvider> Providers { get; } = providers.ToList();

    /// <param name="frames">Evenly spaced instants (one for a reading).</param>
    public async Task<SignalBundle> CollectAsync(
        IReadOnlyList<DateTimeOffset> frames, double latitude, double longitude, NatalChart? natal, CancellationToken ct)
    {
        var step = frames.Count > 1 ? frames[1] - frames[0] : TimeSpan.Zero;
        var query = new SignalQuery(frames[0], frames[^1], latitude, longitude, natal, step);

        var tasks = Providers.Select(async p =>
        {
            try
            {
                var signals = await p.GetAsync(query, ct);
                return (signals, status: new SourceStatus(p.Source, true, signals.Count, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Signal source {Source} failed", p.Source);
                return (signals: (IReadOnlyList<Signal>)[], status: new SourceStatus(p.Source, false, 0, ex.Message));
            }
        }).ToList();
        var results = await Task.WhenAll(tasks);
        foreach (var r in results) board.Report(r.status);

        return new SignalBundle(
            results.SelectMany(r => r.signals).ToList(),
            results.Select(r => r.status).ToList(),
            Providers.ToDictionary(p => p.Source, p => p.SampleValidity));
    }
}
