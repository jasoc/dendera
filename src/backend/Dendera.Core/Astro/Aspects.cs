namespace Dendera.Core.Astro;

/// <summary>An aspect definition. Comes from the rule set JSON, so each "school" can define its own.</summary>
public sealed record AspectSpec(string Id, double Angle, double Orb, string Quality, bool Major = true);

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
