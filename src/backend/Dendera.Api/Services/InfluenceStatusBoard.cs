using System.Collections.Concurrent;

namespace Dendera.Api.Services;

/// <summary>Last known status of every influence (for the catalog page): updated by each <see cref="SignalHub"/> collection.</summary>
public sealed class InfluenceStatusBoard(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, InfluenceStatus> _last = new();

    public void Report(SourceStatus status) =>
        _last[status.Source] = new InfluenceStatus(status.Ok, status.Samples, status.Error, clock.GetUtcNow());

    public InfluenceStatus? Last(string source) => _last.GetValueOrDefault(source);
}

public sealed record InfluenceStatus(bool Ok, int Samples, string? Error, DateTimeOffset CheckedAt);
