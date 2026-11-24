using Dendera.Core.Astro;

namespace Dendera.Core.Signals;

/// <summary>
/// The single currency of the pipeline: every influence (planetary, lunar, solar, geomagnetic,
/// weather...) becomes a Signal sample at an instant.
/// </summary>
/// <param name="Key">Track id, stable over time: "retrograde:mercury", "transit:saturn:square:sun", "kp".</param>
/// <param name="Kind">Signal family that rules match on: see <see cref="SignalKinds"/>.</param>
/// <param name="Source">Who produced it: "ephemeris", "gfz", "open-meteo".</param>
/// <param name="At">Sample instant (UTC).</param>
/// <param name="Intensity">Normalised strength 0..1.</param>
/// <param name="Value">Optional raw value (Kp, hPa, hours, degrees...).</param>
/// <param name="Unit">Unit of <paramref name="Value"/>.</param>
/// <param name="Tags">Structured facts rules can match on (body, sign, aspect, natalPoint, phase...).</param>
/// <param name="Label">Stable, human track name ("Mercurio retrogrado").</param>
/// <param name="Detail">Instant-specific detail ("12°04' Scorpione, -0.62°/g").</param>
public sealed record Signal(
    string Key,
    string Kind,
    string Source,
    DateTimeOffset At,
    double Intensity,
    double? Value,
    string? Unit,
    IReadOnlyDictionary<string, string> Tags,
    string Label,
    string Detail)
{
    public string? Tag(string name) => Tags.TryGetValue(name, out var v) ? v : null;
}

public static class SignalKinds
{
    public const string Placement = "placement";
    public const string Retrograde = "retrograde";
    public const string Station = "station";
    public const string SlowMotion = "slow-motion";
    public const string Decan = "decan";
    public const string FixedStar = "fixed-star";
    public const string PlanetaryHour = "planetary-hour";
    public const string LunarMansion = "lunar-mansion";
    public const string Nakshatra = "nakshatra";
    public const string Septennial = "septennial";
    public const string Profection = "profection";
    public const string Dasha = "dasha";
    public const string Ganzhi = "ganzhi";
    public const string Dignity = "dignity";
    public const string SolarHour = "solar-hour";
    public const string SiderealPlacement = "sidereal-placement";
    public const string SolarWind = "solar-wind";
    public const string ImfBz = "imf-bz";
    public const string XrayFlare = "xray-flare";
    public const string AirQuality = "air-quality";
    public const string Pollen = "pollen";
    public const string Uv = "uv";
    public const string DecumbitureMoon = "decumbiture-moon";
    public const string IllnessDay = "illness-day";
    public const string RetrogradeCount = "retrograde-count";
    public const string SkyAspect = "sky-aspect";
    public const string TransitAspect = "transit-aspect";
    public const string LunarPhase = "lunar-phase";
    public const string LunarDistance = "lunar-distance";
    public const string Photoperiod = "photoperiod";
    public const string PhotoperiodChange = "photoperiod-change";
    public const string Eclipse = "eclipse";
    public const string Geomagnetic = "geomagnetic";
    public const string Pressure = "pressure";
    public const string PressureChange = "pressure-change";
    public const string TemperatureSwing = "temperature-swing";
    public const string Humidity = "humidity";
    public const string Muggy = "muggy";
    public const string DayPhase = "day-phase";
    public const string Dryness = "dryness";
    public const string Wind = "wind";
    public const string Temperature = "temperature";
    public const string Schumann = "schumann";
    public const string SolarTerm = "solar-term";
    public const string OrganClock = "organ-clock";
    public const string HdGate = "hd-gate";

    /// <summary>
    /// Internal, second pass only: a body node's score in the school's own first pass, so a school can write its
    /// relations between nodes ("liver → anger"). Never produced by an influence.
    /// </summary>
    public const string Node = "node";
}

/// <summary>
/// Where and when influences should be sampled, plus the per-profile context computed influences need.
/// Frames are <see cref="From"/>, From+Step, … up to <see cref="To"/> (a single frame when Step is zero).
/// </summary>
public sealed record SignalQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    double Latitude,
    double Longitude,
    NatalChart? Natal = null,
    TimeSpan Step = default,
    IReadOnlyList<DateTimeOffset>? IllnessOnsets = null)
{
    public IEnumerable<DateTimeOffset> Frames()
    {
        if (Step <= TimeSpan.Zero || To <= From)
        {
            yield return From;
            yield break;
        }
        for (var t = From; t <= To; t += Step) yield return t;
    }
}

/// <summary>One signal kind an influence emits, with a one-line Italian description.</summary>
public sealed record InfluenceKind(string Kind, string Description);

/// <summary>
/// What an influence (signal source) is, for the catalog: facts, no interpretation.
/// </summary>
/// <param name="Id">Stable id, equal to <see cref="Signal.Source"/>: "ephemeris", "gfz"…</param>
/// <param name="Name">Italian display name.</param>
/// <param name="Description">Italian, one or two plain sentences: what it measures.</param>
/// <param name="Icon">Icon id in the frontend registry.</param>
/// <param name="ProviderKind"><see cref="ProviderKinds"/>: library, api or computed.</param>
/// <param name="ProviderName">"Astronomy Engine", "GFZ Potsdam Kp API"…</param>
/// <param name="Url">Where the data / library comes from.</param>
/// <param name="License">Data or code license.</param>
/// <param name="Reliability"><see cref="Reliabilities"/>: primary (measured / computed by the source) or derived (third-party processing).</param>
/// <param name="Kinds">Signal kinds it emits.</param>
public sealed record InfluenceInfo(
    string Id,
    string Name,
    string Description,
    string Icon,
    string ProviderKind,
    string ProviderName,
    string Url,
    string License,
    string Reliability,
    IReadOnlyList<InfluenceKind> Kinds)
{
    public bool Emits(string kind) => Kinds.Any(k => k.Kind == kind);
}

public static class ProviderKinds
{
    public const string Library = "library";
    public const string Api = "api";
    public const string Computed = "computed";
}

public static class Reliabilities
{
    public const string Primary = "primary";
    public const string Derived = "derived";
}

/// <summary>
/// An influence: a source of signals (ephemeris, HTTP API, arithmetic on the calendar…).
/// Pure ones live in the Core, impure ones in Dendera.Sources.
/// Adding a new influence to Dendera = writing one of these and registering it.
/// </summary>
public interface ISignalProvider
{
    InfluenceInfo Info { get; }

    string Source => Info.Id;

    /// <summary>
    /// How long a sample stays valid when asking "what is active at instant t".
    /// Zero = frame-aligned: the provider samples exactly on the query frames.
    /// </summary>
    TimeSpan SampleValidity { get; }

    Task<IReadOnlyList<Signal>> GetAsync(SignalQuery query, CancellationToken cancellationToken);
}

public static class SignalSet
{
    /// <summary>For each key, the latest sample at or before <paramref name="at"/> that is still valid.</summary>
    public static IReadOnlyList<Signal> LatestAt(IEnumerable<Signal> samples, DateTimeOffset at, TimeSpan validity) =>
        samples
            .Where(s => s.At <= at && at - s.At <= validity)
            .GroupBy(s => s.Key)
            .Select(g => g.MaxBy(s => s.At)!)
            .ToList();

    /// <summary>
    /// Active signals at an instant across several influences: for each source, the latest valid sample per key
    /// (frame-aligned sources, validity zero, contribute only the samples sitting exactly on the instant).
    /// </summary>
    public static Func<DateTimeOffset, IReadOnlyList<Signal>> ActiveAt(
        IEnumerable<Signal> samples, IReadOnlyDictionary<string, TimeSpan> validityBySource, TimeSpan? fallback = null)
    {
        var bySource = samples.GroupBy(s => s.Source).Select(g =>
        {
            var validity = validityBySource.TryGetValue(g.Key, out var v) ? v : fallback ?? TimeSpan.FromHours(3);
            if (validity <= TimeSpan.Zero)
            {
                var byTime = g.ToLookup(s => s.At);
                return (Func<DateTimeOffset, IEnumerable<Signal>>)(at => byTime[at]);
            }
            var list = g.ToList();
            return at => LatestAt(list, at, validity);
        }).ToList();

        return at => bySource.SelectMany(f => f(at)).ToList();
    }
}
