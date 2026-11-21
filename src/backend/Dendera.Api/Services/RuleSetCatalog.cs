using Dendera.Core.Calibration;
using Dendera.Core.Rules;
using Dendera.Core.Signals;

namespace Dendera.Api.Services;

/// <summary>
/// Loads every school (rules/*.json) at startup and checks it against the registered influences.
/// A broken school file stops the app (fail fast). Each school's quantile table (rules/&lt;id&gt;.quantiles.json,
/// written by the calibration ritual) is loaded next to it; a missing or stale table leaves the school on the
/// fallback scale and marks it "da ricalibrare" in the catalog.
/// </summary>
public sealed class RuleSetCatalog
{
    private readonly Dictionary<string, RuleSet> _sets;
    private readonly Dictionary<string, SchoolCalibration> _calibrations = new();

    public RuleSetCatalog(string directory, IEnumerable<InfluenceInfo> influences, ILogger? log = null)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException($"Rules directory not found: {directory}");
        var known = influences.ToList();
        _sets = Directory.GetFiles(directory, "*.json")
            .Where(f => !f.EndsWith(".quantiles.json", StringComparison.Ordinal))
            .Select(f => RuleSetJson.Parse(File.ReadAllText(f)))
            .OrderBy(s => s.Id, StringComparer.Ordinal)
            .ToDictionary(s => s.Id);
        if (_sets.Count == 0) throw new InvalidOperationException($"No rule sets in {directory}");
        foreach (var set in _sets.Values) RuleSetJson.ValidateInfluences(set, known);

        foreach (var id in _sets.Keys.ToList())
        {
            var set = _sets[id];
            var path = Path.Combine(directory, $"{id}.quantiles.json");
            var calibration = File.Exists(path) ? SchoolCalibration.Parse(File.ReadAllText(path)) : null;
            var state = StateOf(set, calibration);
            if (state == CalibrationState.Calibrated) _calibrations[id] = calibration!;
            else log?.LogWarning("School {School}: quantile table {State}, using the fallback scale until the calibration ritual runs", id, state);
            _sets[id] = set with { Calibration = state };
        }
    }

    /// <summary>Calibrated when the table matches the school's rules hash and the current signals version.</summary>
    public static CalibrationState StateOf(RuleSet set, SchoolCalibration? calibration) =>
        calibration is null ? CalibrationState.Missing
        : calibration.RulesHash == CalibrationHash.Rules(set) && calibration.SignalsVersion == ReferenceSet.SignalsVersion
            ? CalibrationState.Calibrated
            : CalibrationState.Stale;

    public IReadOnlyCollection<RuleSet> All => _sets.Values;

    public RuleSet? Find(string id) => _sets.GetValueOrDefault(id);

    /// <summary>Known schools among <paramref name="ids"/>, in catalog order. Unknown ids are ignored; none → all schools.</summary>
    public IReadOnlyList<RuleSet> Resolve(IEnumerable<string>? ids)
    {
        var wanted = (ids ?? []).Select(i => i.Trim()).Where(_sets.ContainsKey).ToHashSet();
        return wanted.Count == 0 ? _sets.Values.ToList() : _sets.Values.Where(s => wanted.Contains(s.Id)).ToList();
    }

    /// <summary>
    /// The schools ready to evaluate at a place: each carries the reference table that applies there (global, or
    /// the climate zone nearest to the place). Uncalibrated schools come back unchanged (fallback scale).
    /// </summary>
    public IReadOnlyList<RuleSet> ForPlace(IEnumerable<RuleSet> schools, double latitude, double longitude) =>
        schools.Select(s => _calibrations.TryGetValue(s.Id, out var c)
                ? s with { Rarity = c.Table(ClimateZones.TableFor(s, latitude, longitude)) }
                : s)
            .ToList();

    /// <summary>Normalised id list for storage (known ids, catalog order; none → all).</summary>
    public IReadOnlyList<string> Normalize(IEnumerable<string>? ids) => Resolve(ids).Select(s => s.Id).ToList();
}
