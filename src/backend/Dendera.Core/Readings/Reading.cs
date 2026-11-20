using Dendera.Core.Astro;
using Dendera.Core.Rules;
using Dendera.Core.Signals;

namespace Dendera.Core.Readings;

/// <summary>Everything the UI needs for one instant: sky, natal chart, active signals, body zones (all active schools combined).</summary>
public sealed record Reading(
    DateTimeOffset At,
    IReadOnlyList<string> SchoolIds,
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
    /// <summary>Source of combined zone tracks (per-school tracks carry the school id as source).</summary>
    public const string CombinedSource = "schools";

    /// <param name="sky">Current positions.</param>
    /// <param name="signals">All active signals at <paramref name="at"/> (every influence, already merged).</param>
    public static Reading Compose(
        DateTimeOffset at,
        IReadOnlyList<RuleSet> schools,
        NatalBaselines? baselines,
        IReadOnlyList<BodyPosition> sky,
        NatalChart? natal,
        IReadOnlyList<Signal> signals) =>
        new(
            at,
            schools.Select(s => s.Id).ToList(),
            ChartCalculator.PlaceSky(sky, natal?.Cusps),
            natal,
            signals.OrderByDescending(s => s.Intensity).ToList(),
            SchoolEngine.Evaluate(schools, signals, baselines));

    /// <summary>
    /// Builds tracks from samples. <paramref name="frames"/> are the instants where zones are evaluated;
    /// for each frame, active signals are the latest valid samples of every influence (see <see cref="SignalSet.ActiveAt"/>).
    /// Zone tracks: combined ("zone:{zone}") and, when more than one school is active, per school
    /// ("zone:{school}:{zone}") so the correlation engine can compare schools against the journal.
    /// </summary>
    public static Timeline BuildTimeline(
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<RuleSet> schools,
        NatalBaselines? baselines,
        IReadOnlyList<Signal> allSamples,
        IReadOnlyList<DateTimeOffset> frames,
        Func<DateTimeOffset, IReadOnlyList<Signal>> activeAt)
    {
        var signalTracks = ToTracks(allSamples.Where(s => s.Kind != SignalKinds.Placement));

        var tracks = new Dictionary<string, (string source, string label, Dictionary<string, string> tags, List<SignalSample> samples)>();
        void Add(string key, string source, string label, Dictionary<string, string> tags, SignalSample sample)
        {
            if (!tracks.TryGetValue(key, out var t)) tracks[key] = t = (source, label, tags, []);
            t.samples.Add(sample);
        }
        Dictionary<string, string> Tags(ZoneActivation z) =>
            new() { ["zone"] = z.Zone, ["layer"] = z.Layer, ["systemic"] = z.Systemic ? "true" : "false" };

        var perSchoolTracks = schools.Count > 1;
        foreach (var frame in frames)
        {
            var each = SchoolEngine.EvaluateEach(schools, activeAt(frame), baselines);
            foreach (var z in SchoolEngine.Combine(schools, each))
                Add($"zone:{z.Zone}", CombinedSource, z.Label, Tags(z), new SignalSample(frame, z.Score, z.Raw, z.DominantQuality ?? ""));

            if (!perSchoolTracks) continue;
            for (var i = 0; i < schools.Count; i++)
            {
                var school = schools[i];
                foreach (var z in each[i])
                {
                    var tags = Tags(z);
                    tags["school"] = school.Id;
                    Add($"zone:{school.Id}:{z.Zone}", school.Id, $"{z.Label} · {school.Name}", tags,
                        new SignalSample(frame, z.Score, z.Raw, z.DominantQuality ?? ""));
                }
            }
        }

        var zoneTracks = tracks.Select(kv => new SignalTrack(
                kv.Key, "zone", kv.Value.source, kv.Value.label, kv.Value.tags,
                kv.Value.samples.Count == 0 ? 0 : kv.Value.samples.Max(s => s.Intensity),
                kv.Value.samples))
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
