namespace Dendera.Core.Rules;

/// <summary>A node of the body model: a place, a system, a temperament, an emotion or a faculty of the mind.</summary>
/// <param name="Id">Stable id used by rule files and the frontend.</param>
/// <param name="Layer">See <see cref="BodyLayers"/>.</param>
/// <param name="Seat">Where it lives in the body (a soma node), for the 3D view; null = the whole body.</param>
/// <param name="Description">Italian, plain words: what it means, and the tradition it comes from.</param>
public sealed record BodyNode(string Id, string Layer, string? Seat, string Description);

public static class BodyLayers
{
    public const string Soma = "soma";
    public const string Systems = "systems";
    public const string Temperament = "temperament";
    public const string Emotions = "emotions";
    public const string Mind = "mind";
    public const string ZangFu = "zangfu";
    public const string Spirits = "spirits";
    public const string Substances = "substances";
    public const string Chakras = "chakras";
    public const string Nadis = "nadis";
    public const string Domains = "domains";
    public const string Doshas = "doshas";
    public const string Sephiroth = "sephiroth";
    public const string Members = "members";

    public static readonly IReadOnlyList<string> All =
        [Soma, Systems, Temperament, Emotions, Mind, ZangFu, Spirits, Substances, Chakras, Nadis, Domains, Doshas, Sephiroth, Members];
}

/// <summary>
/// The body model: the fixed vocabulary every school writes into (the "where" of a consequence), in layers.
/// Shared by rule files, the 2D body map (src/frontend/src/app/components/body-map.ts) and the 3D body
/// (src/frontend/src/app/components/body-3d/): changing an id means changing all of them.
/// The vocabulary is traditional (Neijing emotions, Galen's temperaments, the medieval cells of the brain)
/// with modern Italian words, so the schools can write their own relations into it.
/// </summary>
public static class BodyZones
{
    public static readonly IReadOnlyList<BodyNode> Nodes =
    [
        // Soma: places on the body, head to feet.
        new("brain", BodyLayers.Soma, null, "Il cervello, sede della mente (governato da Ariete e dalla Luna nella tradizione medievale)."),
        new("head", BodyLayers.Soma, null, "Testa e viso: mal di testa, pressione, tensione."),
        new("eyes", BodyLayers.Soma, null, "Occhi e vista (nella medicina cinese si aprono sul Fegato)."),
        new("ears", BodyLayers.Soma, null, "Orecchie e udito (si aprono sul Rene)."),
        new("nose", BodyLayers.Soma, null, "Naso e olfatto (si apre sul Polmone)."),
        new("mouth", BodyLayers.Soma, null, "Bocca, labbra, lingua e gusto (si aprono su Milza e Cuore)."),
        new("throat", BodyLayers.Soma, null, "Gola, collo, voce."),
        new("shoulders", BodyLayers.Soma, null, "Spalle, braccia, mani."),
        new("lungs", BodyLayers.Soma, null, "Polmoni e respiro."),
        new("heart", BodyLayers.Soma, null, "Cuore e petto."),
        new("stomach", BodyLayers.Soma, null, "Stomaco e milza: digestione, nutrimento."),
        new("solar-plexus", BodyLayers.Soma, null, "Plesso solare: il centro del tronco dove si sentono le emozioni."),
        new("liver", BodyLayers.Soma, null, "Fegato e cistifellea."),
        new("gut", BodyLayers.Soma, null, "Intestino."),
        new("kidneys", BodyLayers.Soma, null, "Reni e zona lombare."),
        new("pelvis", BodyLayers.Soma, null, "Bacino, organi sessuali, eliminazione."),
        new("thighs", BodyLayers.Soma, null, "Anche e cosce."),
        new("knees", BodyLayers.Soma, null, "Ginocchia."),
        new("calves", BodyLayers.Soma, null, "Polpacci e caviglie."),
        new("feet", BodyLayers.Soma, null, "Piedi."),

        // Systems: the whole body.
        new("nervous-system", BodyLayers.Systems, null, "Nervi, reattività, irrequietezza."),
        new("circulation", BodyLayers.Systems, null, "Sangue e circolazione, caldo e freddo."),
        new("skeleton", BodyLayers.Systems, null, "Ossa e articolazioni: la struttura (tessuto del Rene)."),
        new("skin", BodyLayers.Systems, null, "Pelle e peli: il confine con il mondo (tessuto del Polmone)."),
        new("sinews", BodyLayers.Systems, null, "Tendini e legamenti: flessibilità e movimento (tessuto del Fegato)."),
        new("muscles", BodyLayers.Systems, null, "Muscoli e carne: forza e nutrimento (tessuto della Milza)."),
        new("lymph", BodyLayers.Systems, null, "Linfa, liquidi, difese."),
        new("sleep", BodyLayers.Systems, null, "Sonno e ritmo giorno-notte."),
        new("vitality", BodyLayers.Systems, null, "Energia vitale, forza, resistenza."),

        // Temperament: Galen's four humors, each with its planets.
        new("sanguine", BodyLayers.Temperament, "heart",
            "Sanguigno: slancio, socievolezza, ottimismo. Umore: sangue; caldo e umido; Giove e Venere."),
        new("choleric", BodyLayers.Temperament, "liver",
            "Collerico: energia, impazienza, ardore. Umore: bile gialla; caldo e secco; Marte e Sole."),
        new("melancholic", BodyLayers.Temperament, "stomach",
            "Melanconico: tristezza, pesantezza, ritiro, profondità. Umore: bile nera; freddo e secco; Saturno."),
        new("phlegmatic", BodyLayers.Temperament, "lungs",
            "Flemmatico: calma, lentezza, ricettività. Umore: flemma; freddo e umido; Luna."),

        // Emotions: the seven emotions (qi qing) of Chinese medicine, each tied to an organ (Suwen 5 and 39).
        new("anger", BodyLayers.Emotions, "liver", "Collera (nu): irritabilità, impulso, frustrazione. Fa salire il Qi; ferisce il Fegato (Legno)."),
        new("joy", BodyLayers.Emotions, "heart", "Gioia (xi): entusiasmo, apertura; in eccesso agitazione. Rallenta il Qi; ferisce il Cuore (Fuoco)."),
        new("worry", BodyLayers.Emotions, "lungs", "Preoccupazione (you): ansia per ciò che verrà. Annoda il Qi; ferisce Polmone e Milza."),
        new("pensiveness", BodyLayers.Emotions, "stomach", "Rimuginio (si): pensieri che girano, rimuginare. Annoda il Qi; ferisce la Milza (Terra)."),
        new("grief", BodyLayers.Emotions, "lungs", "Tristezza (bei): nostalgia, distacco, pianto. Consuma il Qi; ferisce il Polmone (Metallo)."),
        new("fear", BodyLayers.Emotions, "kidneys", "Paura (kong): ansia, insicurezza, bisogno di protezione. Fa scendere il Qi; ferisce il Rene (Acqua)."),
        new("fright", BodyLayers.Emotions, "heart", "Spavento (jing): lo shock improvviso. Disperde il Qi; colpisce Cuore e Rene."),

        // Mind: the three cells of the brain (medieval ventricle doctrine) and mood.
        new("imagination", BodyLayers.Mind, "brain", "Immaginazione: immagini, sogni, intuizione. Cella anteriore del cervello."),
        new("reason", BodyLayers.Mind, "brain", "Ragione: lucidità, giudizio, concentrazione. Cella mediana del cervello."),
        new("memory", BodyLayers.Mind, "brain", "Memoria: ricordo, continuità. Cella posteriore del cervello."),
        new("mood", BodyLayers.Mind, "heart", "Umore: il tono emotivo di fondo della giornata."),

        // Zang-fu: the twelve organs of Chinese medicine, each with its meridian. Functions, not anatomy.
        new("zf-liver", BodyLayers.ZangFu, "liver", "Fegato (Gan, zang, Legno): fa scorrere il Qi, conserva il Sangue; ospita lo Hun. Meridiano del piede, ore 01–03."),
        new("zf-gallbladder", BodyLayers.ZangFu, "liver", "Vescica biliare (Dan, fu, Legno): decisione e coraggio. Meridiano del piede, ore 23–01."),
        new("zf-heart", BodyLayers.ZangFu, "heart", "Cuore (Xin, zang, Fuoco): governa il Sangue e i vasi; ospita lo Shen. Meridiano della mano, ore 11–13."),
        new("zf-small-intestine", BodyLayers.ZangFu, "gut", "Intestino tenue (Xiaochang, fu, Fuoco): separa il puro dall'impuro. Meridiano della mano, ore 13–15."),
        new("zf-pericardium", BodyLayers.ZangFu, "heart", "Pericardio (Xinbao, Fuoco): protegge il Cuore. Meridiano della mano, ore 19–21."),
        new("zf-triple-burner", BodyLayers.ZangFu, "solar-plexus", "Triplice riscaldatore (Sanjiao, Fuoco): le vie dei liquidi e del Qi nei tre piani del tronco. Meridiano della mano, ore 21–23."),
        new("zf-spleen", BodyLayers.ZangFu, "stomach", "Milza-Pancreas (Pi, zang, Terra): trasforma e trasporta il nutrimento; ospita lo Yi. Meridiano del piede, ore 09–11."),
        new("zf-stomach", BodyLayers.ZangFu, "stomach", "Stomaco (Wei, fu, Terra): riceve e matura il cibo. Meridiano del piede, ore 07–09."),
        new("zf-lung", BodyLayers.ZangFu, "lungs", "Polmone (Fei, zang, Metallo): governa il Qi e il respiro; ospita il Po. Meridiano della mano, ore 03–05."),
        new("zf-large-intestine", BodyLayers.ZangFu, "gut", "Intestino crasso (Dachang, fu, Metallo): lasciar andare. Meridiano della mano, ore 05–07."),
        new("zf-kidney", BodyLayers.ZangFu, "kidneys", "Rene (Shen, zang, Acqua): conserva il Jing, radice di Yin e Yang; ospita lo Zhi. Meridiano del piede, ore 17–19."),
        new("zf-bladder", BodyLayers.ZangFu, "pelvis", "Vescica (Pangguang, fu, Acqua): raccoglie ed elimina i liquidi. Meridiano del piede, ore 15–17."),

        // The five spirits (wu shen), each housed in a zang organ (Suwen 23, Lingshu 8).
        new("shen", BodyLayers.Spirits, "heart", "Shen (lo Spirito): coscienza, presenza, luce degli occhi. Abita il Cuore."),
        new("hun", BodyLayers.Spirits, "liver", "Hun (l'anima eterea): progetti, sogni, direzione della vita. Abita il Fegato."),
        new("po", BodyLayers.Spirits, "lungs", "Po (l'anima corporea): istinto, sensazioni, il corpo nel presente. Abita il Polmone."),
        new("yi", BodyLayers.Spirits, "stomach", "Yi (l'intento): pensiero, concentrazione, studio. Abita la Milza."),
        new("zhi", BodyLayers.Spirits, "kidneys", "Zhi (la volontà): determinazione, forza di andare avanti. Abita il Rene."),

        // Vital substances of Chinese medicine.
        new("qi", BodyLayers.Substances, null, "Qi: il soffio vitale che muove, riscalda, protegge e trasforma."),
        new("xue", BodyLayers.Substances, "heart", "Xue (Sangue): nutre e inumidisce, radica lo Shen; il Cuore lo governa, il Fegato lo conserva."),
        new("yin", BodyLayers.Substances, null, "Yin: ciò che raffredda, nutre, calma e trattiene; la notte e l'inverno."),
        new("yang", BodyLayers.Substances, null, "Yang: ciò che riscalda, muove e attiva; il giorno e l'estate."),
        new("jing", BodyLayers.Substances, "kidneys", "Jing (Essenza): l'eredità vitale conservata nel Rene; crescita, riproduzione, vecchiaia."),
        new("jinye", BodyLayers.Substances, null, "Jinye (Liquidi): i fluidi del corpo, dalla saliva alle articolazioni."),

        // Chakras of tantric yoga (Ṣaṭ-cakra-nirūpaṇa), bottom to top, and the three nadis.
        new("muladhara", BodyLayers.Chakras, "pelvis", "Muladhara (radice): la base del perineo; terra, stabilità, sopravvivenza. Elemento terra."),
        new("svadhisthana", BodyLayers.Chakras, "pelvis", "Svadhisthana (dimora del sé): sotto l'ombelico; acqua, desiderio, piacere, creatività."),
        new("manipura", BodyLayers.Chakras, "solar-plexus", "Manipura (città dei gioielli): l'ombelico; fuoco, volontà, digestione, potere personale."),
        new("anahata", BodyLayers.Chakras, "heart", "Anahata (suono non battuto): il cuore; aria, amore, compassione."),
        new("vishuddha", BodyLayers.Chakras, "throat", "Vishuddha (purissimo): la gola; etere, voce, espressione, verità."),
        new("ajna", BodyLayers.Chakras, "brain", "Ajna (comando): tra le sopracciglia; mente, intuizione, unione di Ida e Pingala."),
        new("sahasrara", BodyLayers.Chakras, "brain", "Sahasrara (loto dai mille petali): la sommità del capo; coscienza oltre gli elementi."),
        new("ida", BodyLayers.Nadis, null, "Ida: la nadi lunare, a sinistra; freschezza, ricettività, mente. Prevale di notte e con la Luna crescente."),
        new("pingala", BodyLayers.Nadis, null, "Pingala: la nadi solare, a destra; calore, azione, corpo. Prevale di giorno."),
        new("sushumna", BodyLayers.Nadis, null, "Sushumna: il canale centrale lungo la colonna; si apre negli istanti di passaggio, all'alba e al tramonto."),

        // Domains of life: the twelve houses of traditional astrology (Valens, Firmicus, Lilly), the person's life
        // rather than the body. Not places: they tint the aura.
        new("domain-1", BodyLayers.Domains, null, "Casa I, Vita: la persona, il corpo, la vitalità, il temperamento."),
        new("domain-2", BodyLayers.Domains, null, "Casa II, Risorse: beni, mezzi, sostentamento."),
        new("domain-3", BodyLayers.Domains, null, "Casa III, Fratelli: fratelli e sorelle, vicini, brevi viaggi, parola."),
        new("domain-4", BodyLayers.Domains, null, "Casa IV, Radici: la casa, il padre, la terra, la fine delle cose."),
        new("domain-5", BodyLayers.Domains, null, "Casa V, Figli: figli, piaceri, creatività, svago."),
        new("domain-6", BodyLayers.Domains, null, "Casa VI, Malattia: salute, fatica quotidiana, servizio."),
        new("domain-7", BodyLayers.Domains, null, "Casa VII, Relazioni: coppia, soci, avversari dichiarati."),
        new("domain-8", BodyLayers.Domains, null, "Casa VIII, Morte: crisi, eredità, beni altrui."),
        new("domain-9", BodyLayers.Domains, null, "Casa IX, Viaggi lunghi: fede, studio, filosofia."),
        new("domain-10", BodyLayers.Domains, null, "Casa X, Azione: lavoro, ruolo pubblico, reputazione."),
        new("domain-11", BodyLayers.Domains, null, "Casa XI, Amici: alleati, speranze, buona sorte."),
        new("domain-12", BodyLayers.Domains, null, "Casa XII, Prove: isolamento, nemici nascosti, ospedali, ritiro."),

        // The three doshas of Ayurveda (Ashtanga Hridaya, Sutrasthana 1 and 12), in their main seat.
        new("vata", BodyLayers.Doshas, "gut", "Vata (aria ed etere): movimento, nervi, respiro; secco, leggero, freddo, mobile. Sede principale: il colon."),
        new("pitta", BodyLayers.Doshas, "solar-plexus", "Pitta (fuoco e acqua): trasformazione, digestione, calore; caldo, acuto, leggero. Sede: tra stomaco e intestino."),
        new("kapha", BodyLayers.Doshas, "lungs", "Kapha (acqua e terra): struttura, coesione, umidità; pesante, freddo, stabile. Sede: il petto."),

        // The ten Sephiroth of the Tree of Life on the body of Adam Kadmon (Agrippa, De occulta philosophia III.10;
        // the Golden Dawn's planetary attributions).
        new("kether", BodyLayers.Sephiroth, "brain", "Kether (Corona): sopra il capo; l'unità, il primo movimento."),
        new("chokmah", BodyLayers.Sephiroth, "brain", "Chokmah (Sapienza): il lato destro del capo; lo zodiaco e le stelle fisse."),
        new("binah", BodyLayers.Sephiroth, "brain", "Binah (Intelligenza): il lato sinistro del capo; Saturno, la forma, il tempo."),
        new("chesed", BodyLayers.Sephiroth, "shoulders", "Chesed (Misericordia): braccio destro; Giove, l'espansione, la generosità."),
        new("geburah", BodyLayers.Sephiroth, "shoulders", "Geburah (Forza): braccio sinistro; Marte, il rigore, il limite."),
        new("tiphereth", BodyLayers.Sephiroth, "heart", "Tiphereth (Bellezza): il cuore; il Sole, l'armonia, il centro."),
        new("netzach", BodyLayers.Sephiroth, "thighs", "Netzach (Vittoria): anca destra; Venere, il desiderio, la natura."),
        new("hod", BodyLayers.Sephiroth, "thighs", "Hod (Gloria): anca sinistra; Mercurio, la parola, l'intelletto."),
        new("yesod", BodyLayers.Sephiroth, "pelvis", "Yesod (Fondamento): i genitali; la Luna, le immagini, la generazione."),
        new("malkuth", BodyLayers.Sephiroth, "feet", "Malkuth (Regno): i piedi; la Terra e i quattro elementi."),

        // Steiner's four members of the human being (Theosophy, 1904; Occult Physiology, GA 128).
        new("physical-body", BodyLayers.Members, null, "Corpo fisico: la forma minerale, ciò che si vede e si pesa."),
        new("etheric-body", BodyLayers.Members, null, "Corpo eterico (vitale): crescita, ritmi, rigenerazione, memoria; lavora soprattutto nel sonno."),
        new("astral-body", BodyLayers.Members, null, "Corpo astrale: sensazioni, desideri, emozioni, coscienza; lascia il corpo nel sonno."),
        new("ego", BodyLayers.Members, null, "Io: l'individualità che dirige gli altri tre e attraversa le vite."),
    ];

    private static readonly Dictionary<string, BodyNode> ById = Nodes.ToDictionary(n => n.Id);

    /// <summary>Every node id, in model order (soma head to feet, then the inner layers).</summary>
    public static readonly IReadOnlyList<string> Ordered = Nodes.Select(n => n.Id).ToList();

    /// <summary>Places on the silhouette, head to feet.</summary>
    public static readonly IReadOnlyList<string> Local = Nodes.Where(n => n.Layer == BodyLayers.Soma).Select(n => n.Id).ToList();

    /// <summary>Everything that is not a place: systems and the inner layers (drawn as gauges in 2D).</summary>
    public static readonly IReadOnlyList<string> Systemic = Nodes.Where(n => n.Layer != BodyLayers.Soma).Select(n => n.Id).ToList();

    public static readonly IReadOnlySet<string> All = Ordered.ToHashSet();

    public static BodyNode Node(string id) => ById[id];

    public static string LayerOf(string id) => ById.TryGetValue(id, out var n) ? n.Layer : BodyLayers.Soma;
}
