namespace Dendera.Core.Astro;

/// <summary>Point identifiers used everywhere (signals, rules JSON, API, UI). Lower-case, stable.</summary>
public static class Bodies
{
    public const string Sun = "sun";
    public const string Moon = "moon";
    public const string Mercury = "mercury";
    public const string Venus = "venus";
    public const string Mars = "mars";
    public const string Jupiter = "jupiter";
    public const string Saturn = "saturn";
    public const string Uranus = "uranus";
    public const string Neptune = "neptune";
    public const string Pluto = "pluto";
    public const string NorthNode = "north-node";
    public const string Ascendant = "asc";
    public const string Midheaven = "mc";

    /// <summary>Everything the ephemeris computes, in traditional order.</summary>
    public static readonly IReadOnlyList<string> All =
        [Sun, Moon, Mercury, Venus, Mars, Jupiter, Saturn, Uranus, Neptune, Pluto, NorthNode];

    /// <summary>Bodies that can be retrograde (true planets).</summary>
    public static readonly IReadOnlyList<string> Planets =
        [Mercury, Venus, Mars, Jupiter, Saturn, Uranus, Neptune, Pluto];

    /// <summary>Mean geocentric speed (deg/day), used to normalise stations and retrograde strength.</summary>
    public static double MeanSpeed(string body) => body switch
    {
        Sun => 0.9856,
        Moon => 13.176,
        Mercury => 1.383,
        Venus => 1.2,
        Mars => 0.524,
        Jupiter => 0.083,
        Saturn => 0.034,
        Uranus => 0.0117,
        Neptune => 0.006,
        Pluto => 0.004,
        NorthNode => 0.053,
        _ => 1.0,
    };

    public static int Order(string body)
    {
        for (var i = 0; i < All.Count; i++) if (All[i] == body) return i;
        return body == Ascendant ? 100 : body == Midheaven ? 101 : 200;
    }
}
