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
}

/// <summary>Where and when external sources should be queried.</summary>
public sealed record SignalQuery(DateTimeOffset From, DateTimeOffset To, double Latitude, double Longitude);

/// <summary>
/// An impure signal source (HTTP, sensors...). Implementations live outside the Core.
/// Adding a new influence to Dendera = writing one of these.
/// </summary>
public interface ISignalProvider
{
    string Source { get; }

    /// <summary>How long a sample stays valid when asking "what is active at instant t".</summary>
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
}
