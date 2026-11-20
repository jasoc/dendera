using Dendera.Core.Readings;

namespace Dendera.Core.Journal;

/// <summary>Daily self-report. Metrics are 1..5; missing metrics are simply absent.</summary>
public sealed record JournalEntry(DateOnly Date, IReadOnlyDictionary<string, int> Metrics, string? Notes);

public static class JournalMetrics
{
    /// <summary>For all metrics, higher = more of it. Note that for "stress" higher is worse.</summary>
    public static readonly IReadOnlyList<string> All =
        ["energy", "mood", "sleep", "focus", "stress", "gut", "head", "body"];
}

public sealed record DailySeries(string Key, string Kind, string Label, IReadOnlyDictionary<DateOnly, double> Values);

public sealed record CorrelationOptions
{
    public int MinDays { get; init; } = 10;
    public int MinActiveDays { get; init; } = 3;
    public double ActiveThreshold { get; init; } = 0.3;
    public int Permutations { get; init; } = 1000;
    public int Seed { get; init; } = 42;
}

/// <param name="Pearson">Correlation between daily signal intensity and the metric.</param>
/// <param name="PValue">Two-sided permutation p-value.</param>
/// <param name="QValue">Benjamini–Hochberg adjusted p-value across all tested pairs (controls false discoveries).</param>
public sealed record CorrelationFinding(
    string Metric,
    string MetricLabel,
    string SignalKey,
    string SignalKind,
    string SignalLabel,
    int Days,
    int ActiveDays,
    double Pearson,
    double MeanWhenActive,
    double MeanWhenQuiet,
    double PValue,
    double QValue,
    string Evidence);

/// <summary>
/// The Babylonian diary engine: does a signal actually move this person's journal?
/// Pure and deterministic (seeded permutations).
/// </summary>
public static class CorrelationEngine
{
    /// <summary>Daily max intensity per track, in local dates (UTC offset applied).</summary>
    public static IReadOnlyList<DailySeries> Daily(IEnumerable<SignalTrack> tracks, TimeSpan utcOffset) =>
        tracks.Select(t => new DailySeries(
                t.Key, t.Kind, t.Label,
                t.Samples
                    .GroupBy(s => DateOnly.FromDateTime(s.At.ToOffset(utcOffset).DateTime))
                    .ToDictionary(g => g.Key, g => g.Max(s => s.Intensity))))
            .ToList();

    public static IReadOnlyList<CorrelationFinding> Analyze(
        IReadOnlyList<JournalEntry> journal,
        IReadOnlyList<DailySeries> series,
        CorrelationOptions? options = null)
    {
        options ??= new CorrelationOptions();
        var raw = new List<(CorrelationFinding f, double p)>();
        var rng = new Random(options.Seed);

        foreach (var metric in JournalMetrics.All)
        {
            var days = journal
                .Where(e => e.Metrics.ContainsKey(metric))
                .Select(e => (e.Date, Value: (double)e.Metrics[metric]))
                .ToList();
            if (days.Count < options.MinDays) continue;

            var y = days.Select(d => d.Value).ToArray();
            foreach (var s in series)
            {
                // A day without a sample means the signal was absent: intensity 0.
                var x = days.Select(d => s.Values.GetValueOrDefault(d.Date)).ToArray();
                var active = x.Count(v => v >= options.ActiveThreshold);
                if (active < options.MinActiveDays || active > x.Length - options.MinActiveDays) continue;

                var r = Pearson(x, y);
                if (double.IsNaN(r)) continue;

                var p = PermutationP(x, y, r, options.Permutations, rng);
                var meanActive = Mean(y.Where((_, i) => x[i] >= options.ActiveThreshold));
                var meanQuiet = Mean(y.Where((_, i) => x[i] < options.ActiveThreshold));

                raw.Add((new CorrelationFinding(
                    metric, Lexicon.Name(metric), s.Key, s.Kind, s.Label, x.Length, active,
                    Math.Round(r, 3), Math.Round(meanActive, 2), Math.Round(meanQuiet, 2),
                    p, 1, ""), p));
            }
        }

        var q = BenjaminiHochberg(raw.Select(r => r.p).ToList());
        return raw
            .Select((r, i) => r.f with { QValue = Math.Round(q[i], 4), PValue = Math.Round(r.p, 4), Evidence = Evidence(r.f.Pearson, q[i]) })
            .OrderBy(f => f.QValue)
            .ThenByDescending(f => Math.Abs(f.Pearson))
            .ToList();
    }

    public static string Evidence(double r, double q) =>
        q < 0.05 && Math.Abs(r) >= 0.3 ? "strong"
        : q < 0.2 && Math.Abs(r) >= 0.2 ? "hint"
        : "none";

    public static double Pearson(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        var n = x.Count;
        double mx = x.Average(), my = y.Average(), sxy = 0, sxx = 0, syy = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = x[i] - mx;
            var dy = y[i] - my;
            sxy += dx * dy;
            sxx += dx * dx;
            syy += dy * dy;
        }
        return sxx == 0 || syy == 0 ? double.NaN : sxy / Math.Sqrt(sxx * syy);
    }

    private static double PermutationP(double[] x, double[] y, double observed, int permutations, Random rng)
    {
        var shuffled = (double[])y.Clone();
        var extreme = 0;
        for (var k = 0; k < permutations; k++)
        {
            rng.Shuffle(shuffled);
            var r = Pearson(x, shuffled);
            if (!double.IsNaN(r) && Math.Abs(r) >= Math.Abs(observed) - 1e-12) extreme++;
        }
        return (extreme + 1.0) / (permutations + 1.0);
    }

    public static double[] BenjaminiHochberg(IReadOnlyList<double> p)
    {
        var m = p.Count;
        var order = Enumerable.Range(0, m).OrderBy(i => p[i]).ToArray();
        var q = new double[m];
        var min = 1.0;
        for (var rank = m; rank >= 1; rank--)
        {
            var i = order[rank - 1];
            min = Math.Min(min, p[i] * m / rank);
            q[i] = Math.Min(1, min);
        }
        return q;
    }

    private static double Mean(IEnumerable<double> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? double.NaN : list.Average();
    }
}
