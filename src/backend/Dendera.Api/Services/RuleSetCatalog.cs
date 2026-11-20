using Dendera.Core.Rules;
using Dendera.Core.Signals;

namespace Dendera.Api.Services;

/// <summary>
/// Loads every school (rules/*.json) at startup and checks it against the registered influences.
/// A broken school file stops the app (fail fast).
/// </summary>
public sealed class RuleSetCatalog
{
    private readonly Dictionary<string, RuleSet> _sets;

    public RuleSetCatalog(string directory, IEnumerable<InfluenceInfo> influences)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException($"Rules directory not found: {directory}");
        var known = influences.ToList();
        _sets = Directory.GetFiles(directory, "*.json")
            .Select(f => RuleSetJson.Parse(File.ReadAllText(f)))
            .OrderBy(s => s.Id, StringComparer.Ordinal)
            .ToDictionary(s => s.Id);
        if (_sets.Count == 0) throw new InvalidOperationException($"No rule sets in {directory}");
        foreach (var set in _sets.Values) RuleSetJson.ValidateInfluences(set, known);
    }

    public IReadOnlyCollection<RuleSet> All => _sets.Values;

    public RuleSet? Find(string id) => _sets.GetValueOrDefault(id);

    /// <summary>Known schools among <paramref name="ids"/>, in catalog order. Unknown ids are ignored; none → all schools.</summary>
    public IReadOnlyList<RuleSet> Resolve(IEnumerable<string>? ids)
    {
        var wanted = (ids ?? []).Select(i => i.Trim()).Where(_sets.ContainsKey).ToHashSet();
        return wanted.Count == 0 ? _sets.Values.ToList() : _sets.Values.Where(s => wanted.Contains(s.Id)).ToList();
    }

    /// <summary>Normalised id list for storage (known ids, catalog order; none → all).</summary>
    public IReadOnlyList<string> Normalize(IEnumerable<string>? ids) => Resolve(ids).Select(s => s.Id).ToList();
}
