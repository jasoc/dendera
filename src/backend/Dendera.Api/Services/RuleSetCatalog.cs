using Dendera.Core.Rules;

namespace Dendera.Api.Services;

/// <summary>Loads every rules/*.json at startup. A broken rule file stops the app (fail fast).</summary>
public sealed class RuleSetCatalog
{
    private readonly Dictionary<string, RuleSet> _sets;

    public RuleSetCatalog(string directory)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException($"Rules directory not found: {directory}");
        _sets = Directory.GetFiles(directory, "*.json")
            .Select(f => RuleSetJson.Parse(File.ReadAllText(f)))
            .ToDictionary(s => s.Id);
        if (_sets.Count == 0) throw new InvalidOperationException($"No rule sets in {directory}");
    }

    public IReadOnlyCollection<RuleSet> All => _sets.Values;

    public RuleSet Get(string? id) =>
        id is not null && _sets.TryGetValue(id, out var set) ? set
        : _sets.TryGetValue("melothesia-classic", out var classic) ? classic
        : _sets.Values.First();
}
