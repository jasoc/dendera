namespace Dendera.Core.Rules;

/// <summary>A family of traditions. Schools of the same lineage, or that borrow from each other, are not independent.</summary>
public sealed record Lineage(string Id, string Name, string Description);

/// <summary>
/// The lineages a school can declare (<see cref="RuleSet.Lineage"/>, <see cref="RuleSet.Parents"/>). Agreement on a
/// node between schools of independent lineages is a real crossing of sources; agreement inside one lineage is one
/// idea counted more than once.
/// </summary>
public static class Lineages
{
    public static readonly IReadOnlyList<Lineage> All =
    [
        new("western-astrology", "Astrologia occidentale", "Ellenistica, araba e medievale: Tolomeo, Valente, Abū Ma'shar, Bonatti, Lilly."),
        new("galenic", "Medicina ippocratico-galenica", "Ippocrate, Galeno, Avicenna: i quattro umori, le stagioni, i sei non naturali."),
        new("chinese-classics", "Classici cinesi", "Huangdi Neijing, Nanjing, I Ching: yin e yang, cinque fasi, zang-fu."),
        new("vedic", "Tradizione vedica", "Jyotish e Ayurveda: Parashara, Varahamihira, Charaka, Vagbhata."),
        new("tantra", "Tantra e yoga", "Chakra, nadi, svara yoga: Ṣaṭ-cakra-nirūpaṇa, Shiva Swarodaya."),
        new("hermetic", "Ermetismo e Cabala", "Corpus Hermeticum, Picatrix, Agrippa, l'Albero della Vita."),
        new("anthroposophy", "Antroposofia", "Rudolf Steiner, sulla scia di Paracelso."),
        new("human-design", "Human Design", "Ra Uru Hu (1987): sintesi moderna di astrologia, I Ching, chakra e Cabala."),
        new("modern-science", "Scienza moderna", "Biometeorologia, cronobiologia: letteratura scientifica peer-reviewed."),
    ];

    private static readonly Dictionary<string, Lineage> ById = All.ToDictionary(l => l.Id);

    public static bool IsKnown(string id) => ById.ContainsKey(id);

    public static Lineage Of(string id) => ById[id];

    /// <summary>Two schools are independent when their lineages differ and neither borrows from the other's.</summary>
    public static bool Independent(RuleSet a, RuleSet b) =>
        a.Lineage != b.Lineage && !a.Parents.Contains(b.Lineage) && !b.Parents.Contains(a.Lineage);
}
