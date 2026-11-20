using Dendera.Core.Astro;
using Dendera.Core.Rules;
using Dendera.Core.Signals;

namespace Dendera.Core.Readings;

/// <summary>Everything the UI needs for one instant: sky, natal chart, active signals, body zones.</summary>
public sealed record Reading(
    DateTimeOffset At,
    string RuleSetId,
    IReadOnlyList<PlacedPoint> Sky,
    NatalChart? Natal,
    IReadOnlyList<Signal> Signals,
    IReadOnlyList<ZoneActivation> Zones);

public sealed record SignalSample(DateTimeOffset At, double Intensity, double? Value, string Detail);

public sealed record SignalTrack(
    string Key,
    string Kind,
    string Source,
    string Label,
    IReadOnlyDictionary<string, string> Tags,
    double Peak,
    IReadOnlyList<SignalSample> Samples);

public sealed record Timeline(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<SignalTrack> Signals,
    IReadOnlyList<SignalTrack> Zones);

public static class ReadingEngine
{
    /// <param name="sky">Current positions.</param>
    /// <param name="signals">All active signals at <paramref name="at"/> (sky + external, already merged).</param>
    public static Reading Compose(
        DateTimeOffset at,
        RuleSet rules,
        IReadOnlyList<BodyPosition> sky,
        NatalChart? natal,
        IReadOnlyList<Signal> signals)
    {
        var baseline = natal is null ? null : RuleEngine.NatalBaseline(rules, natal);
        var zones = RuleEngine.Evaluate(rules, signals, baseline);
        return new Reading(
            at,
            rules.Id,
            ChartCalculator.PlaceSky(sky, natal?.Cusps),
            natal,
            signals.OrderByDescending(s => s.Intensity).ToList(),
            zones);
    }

    /// <summary>
    /// Builds tracks from samples. <paramref name="frames"/> are the instants where zones are evaluated;
    /// for each frame, active signals are the sky samples at that instant plus the latest external samples.
    /// </summary>
    public static Timeline BuildTimeline(
        DateTimeOffset from,
        DateTimeOffset to,
        RuleSet rules,
        NatalChart? natal,
        IReadOnlyList<Signal> allSamples,
        IReadOnlyList<DateTimeOffset> frames,
        Func<DateTimeOffset, IReadOnlyList<Signal>> activeAt)
    {
        var signalTracks = ToTracks(allSamples.Where(s => s.Kind != SignalKinds.Placement));

        var baseline = natal is null ? null : RuleEngine.NatalBaseline(rules, natal);
        var zoneSamples = new Dictionary<string, List<SignalSample>>();
        var zoneLabels = new Dictionary<string, (string label, bool systemic)>();
        foreach (var frame in frames)
        {
            foreach (var z in RuleEngine.Evaluate(rules, activeAt(frame), baseline))
            {
                if (!zoneSamples.TryGetValue(z.Zone, out var list)) zoneSamples[z.Zone] = list = [];
                list.Add(new SignalSample(frame, z.Score, z.Raw, z.DominantQuality ?? ""));
                zoneLabels[z.Zone] = (z.Label, z.Systemic);
            }
        }

        var zoneTracks = zoneSamples.Select(kv => new SignalTrack(
                $"zone:{kv.Key}", "zone", "rules", zoneLabels[kv.Key].label,
                new Dictionary<string, string> { ["zone"] = kv.Key, ["systemic"] = zoneLabels[kv.Key].systemic ? "true" : "false" },
                kv.Value.Count == 0 ? 0 : kv.Value.Max(s => s.Intensity),
                kv.Value))
            .ToList();

        return new Timeline(from, to, signalTracks, zoneTracks);
    }

    public static IReadOnlyList<SignalTrack> ToTracks(IEnumerable<Signal> samples) =>
        samples
            .GroupBy(s => s.Key)
            .Select(g =>
            {
                var ordered = g.OrderBy(s => s.At).ToList();
                var first = ordered[0];
                return new SignalTrack(
                    first.Key, first.Kind, first.Source, first.Label, first.Tags,
                    ordered.Max(s => s.Intensity),
                    ordered.Select(s => new SignalSample(s.At, s.Intensity, s.Value, s.Detail)).ToList());
            })
            .OrderBy(t => KindOrder(t.Kind))
            .ThenByDescending(t => t.Peak)
            .ToList();

    private static int KindOrder(string kind) => kind switch
    {
        SignalKinds.RetrogradeCount => 0,
        SignalKinds.Retrograde => 1,
        SignalKinds.Station => 2,
        SignalKinds.TransitAspect => 3,
        SignalKinds.SkyAspect => 4,
        SignalKinds.Eclipse => 5,
        SignalKinds.LunarPhase => 6,
        SignalKinds.LunarDistance => 7,
        SignalKinds.Photoperiod => 8,
        SignalKinds.PhotoperiodChange => 9,
        SignalKinds.Geomagnetic => 10,
        _ => 20,
    };
}
