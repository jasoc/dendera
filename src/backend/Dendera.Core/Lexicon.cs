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
        ["nervous-system"] = "Sistema nervoso", ["circulation"] = "Circolazione", ["skeleton"] = "Ossa e articolazioni", ["skin"] = "Pelle", ["sinews"] = "Tendini e legamenti", ["muscles"] = "Muscoli e carne",
        ["eyes"] = "Occhi", ["ears"] = "Orecchie", ["nose"] = "Naso", ["mouth"] = "Bocca e lingua",
        ["lymph"] = "Linfa e difese", ["sleep"] = "Sonno e ritmo", ["mood"] = "Umore", ["vitality"] = "Vitalità",
        ["brain"] = "Cervello",
        // temperaments, emotions, mind
        ["sanguine"] = "Sanguigno", ["choleric"] = "Collerico", ["melancholic"] = "Melanconico", ["phlegmatic"] = "Flemmatico",
        ["anger"] = "Collera", ["joy"] = "Gioia", ["worry"] = "Preoccupazione", ["pensiveness"] = "Rimuginio",
        ["grief"] = "Tristezza", ["fear"] = "Paura", ["fright"] = "Spavento",
        ["zf-liver"] = "Fegato · Gan", ["zf-gallbladder"] = "Vescica biliare · Dan", ["zf-heart"] = "Cuore · Xin",
        ["zf-small-intestine"] = "Intestino tenue · Xiaochang", ["zf-pericardium"] = "Pericardio · Xinbao",
        ["zf-triple-burner"] = "Triplice riscaldatore · Sanjiao", ["zf-spleen"] = "Milza-Pancreas · Pi", ["zf-stomach"] = "Stomaco · Wei",
        ["zf-lung"] = "Polmone · Fei", ["zf-large-intestine"] = "Intestino crasso · Dachang", ["zf-kidney"] = "Rene · Shen",
        ["zf-bladder"] = "Vescica · Pangguang",
        ["shen"] = "Shen · Spirito", ["hun"] = "Hun · anima eterea", ["po"] = "Po · anima corporea", ["yi"] = "Yi · intento", ["zhi"] = "Zhi · volontà",
        ["qi"] = "Qi", ["xue"] = "Xue · Sangue", ["yin"] = "Yin", ["yang"] = "Yang", ["jing"] = "Jing · Essenza", ["jinye"] = "Jinye · Liquidi",
        ["muladhara"] = "Muladhara", ["svadhisthana"] = "Svadhisthana", ["manipura"] = "Manipura", ["anahata"] = "Anahata",
        ["vishuddha"] = "Vishuddha", ["ajna"] = "Ajna", ["sahasrara"] = "Sahasrara",
        ["ida"] = "Ida", ["pingala"] = "Pingala", ["sushumna"] = "Sushumna",
        ["imagination"] = "Immaginazione", ["reason"] = "Ragione", ["memory"] = "Memoria",
        // body layers
        ["soma"] = "Corpo", ["systems"] = "Sistemi", ["temperament"] = "Temperamento", ["emotions"] = "Emozioni", ["mind"] = "Mente",
        ["zangfu"] = "Organi zang-fu e meridiani", ["spirits"] = "Cinque Shen", ["substances"] = "Sostanze vitali",
        ["chakras"] = "Chakra", ["nadis"] = "Nadi",
        // qualities
        ["tension"] = "tensione", ["flow"] = "flusso", ["fusion"] = "fusione", ["inward"] = "introversione",
        ["agitation"] = "agitazione", ["depletion"] = "svuotamento", ["pressure"] = "pressione", ["sensitivity"] = "sensibilità", ["neutral"] = "neutra",
        // Chinese medicine: organs, five elements, seasons, the 24 solar terms
        ["lung"] = "Polmone", ["large-intestine"] = "Intestino crasso", ["spleen"] = "Milza-Pancreas",
        ["small-intestine"] = "Intestino tenue", ["bladder"] = "Vescica", ["kidney"] = "Rene",
        ["pericardium"] = "Pericardio", ["triple-burner"] = "Triplice riscaldatore", ["gallbladder"] = "Vescica biliare",
        ["wood"] = "Legno", ["fire"] = "Fuoco", ["earth"] = "Terra", ["metal"] = "Metallo", ["water"] = "Acqua",
        ["spring"] = "primavera", ["summer"] = "estate", ["late-summer"] = "tarda estate", ["autumn"] = "autunno", ["winter"] = "inverno",
        ["lichun"] = "Lichun (inizio della primavera)", ["yushui"] = "Yushui (acqua piovana)",
        ["jingzhe"] = "Jingzhe (risveglio degli insetti)", ["chunfen"] = "Chunfen (equinozio di primavera)",
        ["qingming"] = "Qingming (luce pura)", ["guyu"] = "Guyu (pioggia dei cereali)",
        ["lixia"] = "Lixia (inizio dell'estate)", ["xiaoman"] = "Xiaoman (piccola pienezza)",
        ["mangzhong"] = "Mangzhong (grano in spiga)", ["xiazhi"] = "Xiazhi (solstizio d'estate)",
        ["xiaoshu"] = "Xiaoshu (piccolo caldo)", ["dashu"] = "Dashu (grande caldo)",
        ["liqiu"] = "Liqiu (inizio dell'autunno)", ["chushu"] = "Chushu (fine del caldo)",
        ["bailu"] = "Bailu (rugiada bianca)", ["qiufen"] = "Qiufen (equinozio d'autunno)",
        ["hanlu"] = "Hanlu (rugiada fredda)", ["shuangjiang"] = "Shuangjiang (discesa della brina)",
        ["lidong"] = "Lidong (inizio dell'inverno)", ["xiaoxue"] = "Xiaoxue (piccola neve)",
        ["daxue"] = "Daxue (grande neve)", ["dongzhi"] = "Dongzhi (solstizio d'inverno)",
        ["xiaohan"] = "Xiaohan (piccolo freddo)", ["dahan"] = "Dahan (grande freddo)",
        // weather levels
        ["cold"] = "freddo", ["heat"] = "caldo", ["mild"] = "mite", ["low"] = "bassa", ["high"] = "alta", ["normal"] = "normale",
        ["day"] = "giorno", ["night"] = "notte", ["dawn"] = "alba", ["dusk"] = "tramonto", ["twilight"] = "crepuscolo",
        ["shukla"] = "metà chiara (shukla paksha)", ["krishna"] = "metà scura (krishna paksha)",
        ["calm"] = "calmo", ["breezy"] = "brezza", ["windy"] = "ventoso", ["strong"] = "forte",
        // Human Design: extra activation points and the nine centers
        ["south-node"] = "Nodo Sud",
        ["hd-head"] = "centro della Testa", ["hd-ajna"] = "centro Ajna", ["hd-throat"] = "centro della Gola",
        ["hd-g"] = "centro G (Sé)", ["hd-ego"] = "centro del Cuore (Ego)", ["hd-sacral"] = "centro Sacrale",
        ["hd-solar-plexus"] = "centro del Plesso solare", ["hd-spleen"] = "centro della Milza", ["hd-root"] = "centro della Radice",
        ["defined"] = "definito", ["open"] = "aperto", ["drop"] = "calo", ["rise"] = "salita",
        // journal metrics
        ["energy"] = "Energia", ["focus"] = "Lucidità", ["stress"] = "Stress", ["body"] = "Corpo",
    };

    /// <summary>Signal kinds (separate table: some kind ids collide with other ids, e.g. "pressure").</summary>
    private static readonly Dictionary<string, string> Kinds = new()
    {
        ["placement"] = "Posizione nei segni", ["retrograde"] = "Moto retrogrado", ["station"] = "Stazione", ["slow-motion"] = "Moto lento",
        ["retrograde-count"] = "Numero di retrogradi", ["sky-aspect"] = "Aspetto nel cielo",
        ["transit-aspect"] = "Transito sul tema natale", ["lunar-phase"] = "Fase lunare",
        ["lunar-distance"] = "Distanza della Luna", ["photoperiod"] = "Ore di luce",
        ["photoperiod-change"] = "Variazione della luce", ["eclipse"] = "Eclissi",
        ["geomagnetic"] = "Attività geomagnetica", ["pressure"] = "Pressione atmosferica",
        ["pressure-change"] = "Variazione di pressione", ["temperature-swing"] = "Sbalzo termico",
        ["humidity"] = "Umidità", ["dryness"] = "Aria secca", ["muggy"] = "Afa", ["day-phase"] = "Giorno e notte", ["wind"] = "Vento", ["temperature"] = "Temperatura",
        ["schumann"] = "Risonanza di Schumann", ["solar-term"] = "Termine solare cinese",
        ["organ-clock"] = "Orologio degli organi", ["hd-gate"] = "Porta di Human Design", ["zone"] = "Zona del corpo",
        ["node"] = "Organo o nodo del corpo (secondo passaggio)",
    };

    /// <summary>Signal tag names, as used in rule descriptions ("zone corrispondenti a: corpo celeste, segno").</summary>
    private static readonly Dictionary<string, string> Tags = new()
    {
        ["body"] = "corpo celeste", ["sign"] = "segno", ["bodyA"] = "primo corpo", ["bodyB"] = "secondo corpo",
        ["transitBody"] = "corpo in transito", ["natalPoint"] = "punto natale", ["natalSign"] = "segno del punto natale",
        ["transitSign"] = "segno del transito", ["aspect"] = "aspetto", ["phase"] = "fase", ["direction"] = "direzione",
        ["level"] = "livello", ["type"] = "tipo", ["organ"] = "organo", ["element"] = "elemento",
        ["term"] = "termine solare", ["paksha"] = "metà del mese lunare", ["tithi"] = "tithi", ["light"] = "luce", ["season"] = "stagione", ["center"] = "centro", ["gate"] = "porta",
        ["natalCenter"] = "centro nel tema natale", ["node"] = "nodo del corpo", ["channel"] = "canale", ["channelCenter"] = "centro collegato",
    };

    public static string Name(string id) => It.TryGetValue(id, out var name) ? name : id;

    public static string Kind(string kind) => Kinds.TryGetValue(kind, out var name) ? name : Name(kind);

    public static string Tag(string tag) => Tags.TryGetValue(tag, out var name) ? name : tag;
}
