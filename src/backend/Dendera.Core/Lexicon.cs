namespace Dendera.Core;

/// <summary>
/// Italian display names for every stable id (bodies, signs, aspects, zones, metrics...).
/// UI language is Italian for now; i18n is deferred, so all user-facing names live here.
/// </summary>
public static class Lexicon
{
    private static readonly Dictionary<string, string> It = new()
    {
        // bodies & points
        ["sun"] = "Sole", ["moon"] = "Luna", ["mercury"] = "Mercurio", ["venus"] = "Venere",
        ["mars"] = "Marte", ["jupiter"] = "Giove", ["saturn"] = "Saturno", ["uranus"] = "Urano",
        ["neptune"] = "Nettuno", ["pluto"] = "Plutone", ["north-node"] = "Nodo Nord",
        ["asc"] = "Ascendente", ["mc"] = "Medio Cielo",
        // signs
        ["aries"] = "Ariete", ["taurus"] = "Toro", ["gemini"] = "Gemelli", ["cancer"] = "Cancro",
        ["leo"] = "Leone", ["virgo"] = "Vergine", ["libra"] = "Bilancia", ["scorpio"] = "Scorpione",
        ["sagittarius"] = "Sagittario", ["capricorn"] = "Capricorno", ["aquarius"] = "Acquario", ["pisces"] = "Pesci",
        // aspects
        ["conjunction"] = "congiunzione", ["opposition"] = "opposizione", ["square"] = "quadratura",
        ["trine"] = "trigono", ["sextile"] = "sestile", ["quincunx"] = "quinconce",
        ["semisquare"] = "semiquadrato", ["sesquiquadrate"] = "sesquiquadrato",
        // lunar phases
        ["new"] = "Luna nuova", ["waxing-crescent"] = "Luna crescente", ["first-quarter"] = "Primo quarto",
        ["waxing-gibbous"] = "Gibbosa crescente", ["full"] = "Luna piena", ["waning-gibbous"] = "Gibbosa calante",
        ["last-quarter"] = "Ultimo quarto", ["waning-crescent"] = "Luna calante",
        // body zones
        ["head"] = "Testa", ["throat"] = "Gola", ["shoulders"] = "Spalle e braccia", ["lungs"] = "Polmoni",
        ["stomach"] = "Stomaco", ["heart"] = "Cuore", ["solar-plexus"] = "Plesso solare", ["liver"] = "Fegato",
        ["gut"] = "Intestino", ["kidneys"] = "Reni e lombari", ["pelvis"] = "Bacino", ["thighs"] = "Anche e cosce",
        ["knees"] = "Ginocchia", ["calves"] = "Polpacci e caviglie", ["feet"] = "Piedi",
        ["nervous-system"] = "Sistema nervoso", ["circulation"] = "Circolazione", ["skeleton"] = "Ossa e pelle",
        ["lymph"] = "Linfa e difese", ["sleep"] = "Sonno e ritmo", ["mood"] = "Umore", ["vitality"] = "Vitalità",
        // qualities
        ["tension"] = "tensione", ["flow"] = "flusso", ["fusion"] = "fusione", ["inward"] = "introversione",
        ["agitation"] = "agitazione", ["depletion"] = "svuotamento", ["pressure"] = "pressione", ["sensitivity"] = "sensibilità", ["neutral"] = "neutra",
        // journal metrics
        ["energy"] = "Energia", ["focus"] = "Lucidità", ["stress"] = "Stress", ["body"] = "Corpo",
    };

    public static string Name(string id) => It.TryGetValue(id, out var name) ? name : id;
}
