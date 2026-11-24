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
        ["domains"] = "Domini di vita", ["doshas"] = "Dosha", ["sephiroth"] = "Sephiroth", ["members"] = "Arti costitutivi",
        ["domain-1"] = "Vita (I)", ["domain-2"] = "Risorse (II)", ["domain-3"] = "Fratelli (III)", ["domain-4"] = "Radici (IV)",
        ["domain-5"] = "Figli e piaceri (V)", ["domain-6"] = "Malattia (VI)", ["domain-7"] = "Relazioni (VII)",
        ["domain-8"] = "Crisi ed eredità (VIII)", ["domain-9"] = "Viaggi e fede (IX)", ["domain-10"] = "Azione (X)",
        ["domain-11"] = "Amici (XI)", ["domain-12"] = "Prove (XII)",
        ["vata"] = "Vata", ["pitta"] = "Pitta", ["kapha"] = "Kapha",
        ["kether"] = "Kether", ["chokmah"] = "Chokmah", ["binah"] = "Binah", ["chesed"] = "Chesed", ["geburah"] = "Geburah",
        ["tiphereth"] = "Tiphereth", ["netzach"] = "Netzach", ["hod"] = "Hod", ["yesod"] = "Yesod", ["malkuth"] = "Malkuth",
        ["physical-body"] = "Corpo fisico", ["etheric-body"] = "Corpo eterico", ["astral-body"] = "Corpo astrale", ["ego"] = "Io",
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
        // Lunar mansions (Agrippa, De occulta philosophia II.33)
        ["alnath"] = "Alnath", ["allothaim"] = "Allothaim", ["achaomazon"] = "Achaomazon (Pleiadi)",
        ["aldebaran-mansion"] = "Aldebaran", ["alchatay"] = "Alchatay", ["alhanna"] = "Alhanna", ["aldimiach"] = "Aldimiach",
        ["alnaza"] = "Alnaza", ["archaam"] = "Archaam", ["algelioche"] = "Algelioche", ["azobra"] = "Azobra",
        ["alzarpha"] = "Alzarpha", ["alhaire"] = "Alhaire", ["achureth"] = "Achureth", ["agrapha"] = "Agrapha",
        ["azubene"] = "Azubene", ["alchil"] = "Alchil", ["alchas"] = "Alchas", ["allatha"] = "Allatha",
        ["abnahaya"] = "Abnahaya", ["abeda"] = "Abeda", ["sadahacha"] = "Sadahacha", ["sadabath"] = "Sadabath",
        ["sadabeth"] = "Sadabeth", ["sadalabra"] = "Sadalabra", ["alpharg"] = "Alpharg", ["alcharya"] = "Alcharya",
        ["albotham"] = "Albotham",
        // Nakshatras and the lunar nodes of Jyotish
        ["ashvini"] = "Ashvini", ["bharani"] = "Bharani", ["krittika"] = "Krittika", ["rohini"] = "Rohini",
        ["mrigashira"] = "Mrigashira", ["ardra"] = "Ardra", ["punarvasu"] = "Punarvasu", ["pushya"] = "Pushya",
        ["ashlesha"] = "Ashlesha", ["magha"] = "Magha", ["purva-phalguni"] = "Purva Phalguni",
        ["uttara-phalguni"] = "Uttara Phalguni", ["hasta"] = "Hasta", ["chitra"] = "Chitra", ["swati"] = "Swati",
        ["vishakha"] = "Vishakha", ["anuradha"] = "Anuradha", ["jyeshtha"] = "Jyeshtha", ["mula"] = "Mula",
        ["purva-ashadha"] = "Purva Ashadha", ["uttara-ashadha"] = "Uttara Ashadha", ["shravana"] = "Shravana",
        ["dhanishta"] = "Dhanishta", ["shatabhisha"] = "Shatabhisha", ["purva-bhadrapada"] = "Purva Bhadrapada",
        ["uttara-bhadrapada"] = "Uttara Bhadrapada", ["revati"] = "Revati", ["rahu"] = "Rahu", ["ketu"] = "Ketu",
        // Fixed stars
        ["algol"] = "Algol", ["alcyone"] = "Alcione (Pleiadi)", ["aldebaran"] = "Aldebaran", ["capella"] = "Capella",
        ["sirius"] = "Sirio", ["procyon"] = "Procione", ["regulus"] = "Regolo", ["alkaid"] = "Alkaid",
        ["algorab"] = "Algorab", ["spica"] = "Spica", ["arcturus"] = "Arturo", ["alphecca"] = "Alphecca",
        ["antares"] = "Antares", ["vega"] = "Vega", ["deneb-algedi"] = "Deneb Algedi", ["fomalhaut"] = "Fomalhaut",
        // Heavenly stems and earthly branches
        ["stem-jia"] = "Jia (Legno yang)", ["stem-yi"] = "Yi (Legno yin)", ["stem-bing"] = "Bing (Fuoco yang)",
        ["stem-ding"] = "Ding (Fuoco yin)", ["stem-wu"] = "Wu (Terra yang)", ["stem-ji"] = "Ji (Terra yin)",
        ["stem-geng"] = "Geng (Metallo yang)", ["stem-xin"] = "Xin (Metallo yin)", ["stem-ren"] = "Ren (Acqua yang)",
        ["stem-gui"] = "Gui (Acqua yin)",
        ["branch-zi"] = "Zi (Topo)", ["branch-chou"] = "Chou (Bue)", ["branch-yin"] = "Yin (Tigre)", ["branch-mao"] = "Mao (Coniglio)",
        ["branch-chen"] = "Chen (Drago)", ["branch-si"] = "Si (Serpente)", ["branch-wu"] = "Wu (Cavallo)", ["branch-wei"] = "Wei (Capra)",
        ["branch-shen"] = "Shen (Scimmia)", ["branch-you"] = "You (Gallo)", ["branch-xu"] = "Xu (Cane)", ["branch-hai"] = "Hai (Maiale)",
        ["domicile"] = "in domicilio", ["exaltation"] = "in esaltazione", ["detriment"] = "in esilio", ["fall"] = "in caduta",
        ["dignified"] = "dignitoso", ["debilitated"] = "debilitato",
        ["slow"] = "lento", ["fast"] = "veloce", ["very-fast"] = "molto veloce", ["south"] = "verso sud", ["north"] = "verso nord",
        ["pm2_5"] = "PM2,5", ["pm10"] = "PM10", ["ozone"] = "ozono", ["nitrogen_dioxide"] = "biossido di azoto",
        ["within"] = "entro la linea guida", ["above"] = "sopra la linea guida", ["far-above"] = "oltre il doppio della linea guida",
        ["grass_pollen"] = "graminacee", ["birch_pollen"] = "betulla", ["olive_pollen"] = "olivo", ["ragweed_pollen"] = "ambrosia",
        ["alder_pollen"] = "ontano", ["mugwort_pollen"] = "artemisia",
        ["moderate"] = "moderato", ["very-high"] = "molto alto", ["extreme"] = "estremo",
        ["crisis"] = "giorno di crisi", ["judicial"] = "giorno indicativo",
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
        ["solar-wind"] = "Vento solare", ["imf-bz"] = "Campo magnetico interplanetario", ["xray-flare"] = "Brillamenti solari",
        ["air-quality"] = "Qualità dell'aria", ["pollen"] = "Pollini", ["uv"] = "Indice UV",
        ["decumbiture-moon"] = "Luna dalla decombitura", ["illness-day"] = "Giorni dall'inizio del malessere",
        ["dignity"] = "Dignità essenziale", ["solar-hour"] = "Ora solare locale", ["sidereal-placement"] = "Posizione siderale",
        ["decan"] = "Decano", ["fixed-star"] = "Stella fissa", ["planetary-hour"] = "Ora planetaria",
        ["lunar-mansion"] = "Dimora lunare", ["nakshatra"] = "Nakshatra", ["septennial"] = "Settennio",
        ["profection"] = "Profezione annuale", ["dasha"] = "Dasha vimshottari", ["ganzhi"] = "Calendario sessagesimale",
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
        ["ruler"] = "pianeta dell'ora", ["dayRuler"] = "pianeta del giorno", ["hour"] = "ora", ["decan"] = "decano",
        ["face"] = "pianeta della faccia", ["star"] = "stella", ["royal"] = "stella regale", ["mansion"] = "dimora",
        ["number"] = "numero", ["nakshatra"] = "nakshatra", ["pada"] = "pada", ["lord"] = "signore",
        ["subLord"] = "sotto-signore", ["septennial"] = "settennio", ["year"] = "anno", ["house"] = "casa",
        ["yearStem"] = "tronco dell'anno", ["yearBranch"] = "ramo dell'anno", ["dayStem"] = "tronco del giorno",
        ["dayBranch"] = "ramo del giorno", ["hourBranch"] = "ramo dell'ora", ["dayElement"] = "elemento del giorno",
        ["yearElement"] = "elemento dell'anno", ["dignity"] = "dignità", ["condition"] = "condizione", ["watch"] = "fascia oraria", ["class"] = "classe", ["pollutant"] = "inquinante",
        ["moonAspect"] = "aspetto della Luna", ["day"] = "giorno", ["critical"] = "giorno critico",
    };

    public static string Name(string id) => It.TryGetValue(id, out var name) ? name : id;

    public static string Kind(string kind) => Kinds.TryGetValue(kind, out var name) ? name : Name(kind);

    public static string Tag(string tag) => Tags.TryGetValue(tag, out var name) ? name : tag;
}
