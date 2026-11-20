namespace Dendera.Core.Astro;

/// <summary>An aspect definition: angle, base orb, traditional quality.</summary>
public sealed record AspectSpec(string Id, double Angle, double Orb, string Quality, bool Major = true);

/// <summary>
/// The aspects the ephemeris influence detects. Facts are computed once for every school; a school that wants
/// only Ptolemaic aspects filters on the signal's "aspect" tag in its rules.
/// </summary>
public static class AspectCatalog
{
    public static readonly IReadOnlyList<AspectSpec> Default =
    [
        new("conjunction", 0, 8, "fusion"),
        new("opposition", 180, 8, "tension"),
        new("square", 90, 7, "tension"),
        new("trine", 120, 7, "flow"),
        new("sextile", 60, 5, "flow"),
        new("quincunx", 150, 3, "tension", Major: false),
        new("semisquare", 45, 2, "tension", Major: false),
        new("sesquiquadrate", 135, 2, "tension", Major: false),
    ];

    /// <summary>Multiplier applied to base orbs for transits to natal points.</summary>
    public const double TransitOrbFactor = 0.4;

    /// <summary>Multiplier applied to base orbs for planet-to-planet aspects in the sky (major aspects only).</summary>
    public const double SkyOrbFactor = 0.5;
}

public sealed record AspectHit(AspectSpec Aspect, double Deviation, double OrbUsed, bool Applying)
{
    /// <summary>1 at exactness, 0 at the edge of the orb (cosine-shaped, so the peak is broad).</summary>
    public double Strength => Math.Cos(Math.Min(1.0, Deviation / OrbUsed) * Math.PI / 2);
}

public static class AspectFinder
{
    /// <summary>
    /// Closest aspect between a moving point A (with speed in deg/day) and a point B (with its own speed).
    /// </summary>
    public static AspectHit? Find(
        double lonA, double speedA, double lonB, double speedB,
        IReadOnlyList<AspectSpec> specs, double orbFactor, bool majorOnly = false)
    {
        var sep = Zodiac.Separation(lonA, lonB);
        AspectHit? best = null;

        foreach (var spec in specs)
        {
            if (majorOnly && !spec.Major) continue;
            var orb = spec.Orb * orbFactor;
            var dev = Math.Abs(sep - spec.Angle);
            if (dev > orb) continue;
            if (best is not null && dev / orb >= best.Deviation / best.OrbUsed) continue;

            const double dt = 0.01;
            var later = Zodiac.Separation(lonA + speedA * dt, lonB + speedB * dt);
            var applying = Math.Abs(later - spec.Angle) < dev;
            best = new AspectHit(spec, dev, orb, applying);
        }

        return best;
    }
}
