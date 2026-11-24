using Dendera.Core.Astro;

namespace Dendera.Core.Signals;

/// <summary>
/// The sky as an influence: wraps the pure <see cref="SkySignalGenerator"/>. Samples sit exactly on the query
/// frames, so <see cref="SampleValidity"/> is zero (a vanished aspect must not linger into the next frame).
/// </summary>
public sealed class EphemerisProvider(IEphemeris ephemeris) : ISignalProvider
{
    public const string Id = "ephemeris";

    public static readonly InfluenceInfo Influence = new(
        Id,
        "Effemeridi (cielo)",
        "Posizioni di Sole, Luna e pianeti calcolate a ogni istante: segni, moto retrogrado e stazioni, " +
        "aspetti tra pianeti e verso il tema natale, fasi e distanza della Luna, ore di luce, giorno e notte, eclissi.",
        "ephemeris",
        ProviderKinds.Library,
        "Astronomy Engine",
        "https://github.com/cosinekitty/astronomy",
        "MIT",
        Reliabilities.Primary,
        [
            new(SignalKinds.Placement, "Segno zodiacale in cui si trova ogni corpo celeste"),
            new(SignalKinds.Dignity, "Dignità essenziale per segno dei sette pianeti classici: domicilio, esaltazione, esilio, caduta (punti di Lilly)"),
            new(SignalKinds.SolarHour, "Ora solare locale e fascia di quattro ore del giorno"),
            new(SignalKinds.Decan, "Decano (terzo di segno, 10°) di ogni corpo e il pianeta della sua faccia, in ordine caldeo"),
            new(SignalKinds.FixedStar, "Corpo congiunto (entro 1°) a una stella regale o a una delle quindici stelle behenie di Agrippa"),
            new(SignalKinds.Retrograde, "Pianeta in moto apparente all'indietro visto dalla Terra (debilità −5 nella tavola di Lilly)"),
            new(SignalKinds.SlowMotion, "Pianeta diretto ma più lento del suo moto medio (debilità −2 nella tavola di Lilly)"),
            new(SignalKinds.Station, "Pianeta quasi fermo nel cielo, al punto di inversione del moto"),
            new(SignalKinds.RetrogradeCount, "Quanti pianeti sono retrogradi nello stesso momento"),
            new(SignalKinds.SkyAspect, "Angolo significativo tra due corpi nel cielo attuale (aspetti maggiori)"),
            new(SignalKinds.TransitAspect, "Angolo significativo tra un corpo di oggi e un punto del tema natale"),
            new(SignalKinds.LunarPhase, "Fase della Luna (con tithi e metà chiara/scura del mese lunare indiano); più intensa a Luna nuova e Luna piena"),
            new(SignalKinds.LunarDistance, "Distanza Terra-Luna; più intensa al perigeo"),
            new(SignalKinds.Photoperiod, "Ore di luce del giorno nel luogo attuale; più intensa quando la luce scarseggia"),
            new(SignalKinds.PhotoperiodChange, "Velocità con cui le giornate si allungano o si accorciano"),
            new(SignalKinds.DayPhase, "Giorno, notte, alba o tramonto nel luogo attuale, dall'altezza del Sole sull'orizzonte"),
            new(SignalKinds.Eclipse, "Finestra di ±15 giorni attorno a un'eclissi di Sole o di Luna"),
        ]);

    private readonly SkySignalGenerator _generator = new(ephemeris);

    public InfluenceInfo Info => Influence;

    public string Source => Id;

    public TimeSpan SampleValidity => TimeSpan.Zero;

    public Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken)
    {
        var ctx = new SkyContext(query.Latitude, query.Longitude, query.Natal);
        var signals = query.Step <= TimeSpan.Zero || query.To <= query.From
            ? _generator.At(query.From, ctx)
            : _generator.Range(query.From, query.To, query.Step, ctx);
        return Task.FromResult(signals);
    }
}
