namespace Dendera.Core.Rules;

/// <summary>
/// Fixed body-zone vocabulary shared by rule files and the web body map
/// (src/frontend/src/app/body-map). Changing an id means changing both.
/// </summary>
public static class BodyZones
{
    /// <summary>Zones with a place on the silhouette, head to feet.</summary>
    public static readonly IReadOnlyList<string> Local =
    [
        "head", "throat", "shoulders", "lungs", "heart", "stomach", "solar-plexus", "liver",
        "gut", "kidneys", "pelvis", "thighs", "knees", "calves", "feet",
    ];

    /// <summary>Whole-body systems, drawn as an aura / side gauges.</summary>
    public static readonly IReadOnlyList<string> Systemic =
    [
        "nervous-system", "circulation", "skeleton", "lymph", "sleep", "mood", "vitality",
    ];

    public static readonly IReadOnlySet<string> All = Local.Concat(Systemic).ToHashSet();
}
