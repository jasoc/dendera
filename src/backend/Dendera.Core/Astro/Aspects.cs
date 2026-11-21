namespace Dendera.Core.Astro;

/// <summary>An aspect definition: angle and traditional quality. Orbs belong to the planets, not to the aspect.</summary>
public sealed record AspectSpec(string Id, double Angle, string Quality, bool Major = true);

/// <summary>
/// The aspects the ephemeris influence detects. Facts are computed once for every school; a school that wants
/// only Ptolemaic aspects filters on the signal's "aspect" tag in its rules.
/// </summary>
public static class AspectCatalog
{
    /// <summary>
    /// The five Ptolemaic aspects (Ptolemy, Tetrabiblos I.13) and three minor ones kept by decision of the
    /// calibration plan (quincunx, semisquare, sesquiquadrate; Kepler, De fundamentis astrologiae certioribus, 1601).
    /// </summary>
    public static readonly IReadOnlyList<AspectSpec> Default =
    [
        new("conjunction", 0, "fusion"),
        new("opposition", 180, "tension"),
        new("square", 90, "tension"),
        new("trine", 120, "flow"),
        new("sextile", 60, "flow"),
        new("quincunx", 150, "tension", Major: false),
        new("semisquare", 45, "tension", Major: false),
        new("sesquiquadrate", 135, "tension", Major: false),
    ];

    /// <summary>
    /// Minor aspects have no traditional orb: they get half the planets' orb. Convention of the instrument
    /// (docs/plans/calibration.md, step 1 A), not a citation.
    /// </summary>
    public const double MinorOrbFactor = 0.5;

    /// <summary>
    /// Orb ("light") of each body in degrees: al-Bīrūnī, The Book of Instruction in the Elements of the Art of
    /// Astrology (1029), trans. R. Ramsay Wright (1934), §§ 436–437, the list Lilly also uses (Christian Astrology,
    /// 1647, p. 107). Outer planets, nodes and angles: 5°, the later extension to "non-luminous points"
    /// (Ptolemy gives 5° to the Ascendant).
    /// </summary>
    public static double PlanetOrb(string body) => body switch
    {
        Bodies.Sun => 15,
        Bodies.Moon => 12,
        Bodies.Mercury => 7,
        Bodies.Venus => 7,
        Bodies.Mars => 8,
        Bodies.Jupiter => 9,
        Bodies.Saturn => 9,
        _ => 5,
    };

    /// <summary>Half the orb: Dariot (1557), two bodies are in aspect within the sum of their moieties.</summary>
    public static double Moiety(string body) => PlanetOrb(body) / 2;

    /// <summary>Orb of a planet-to-planet aspect in the sky: the sum of the two moieties.</summary>
    public static double SkyOrb(string bodyA, string bodyB) => Moiety(bodyA) + Moiety(bodyB);

    /// <summary>
    /// Orb of a transit to a natal point: the transiting planet's moiety only, the natal point counts as a point
    /// (owner's decision B.2 of the calibration plan: no source gives a separate number for transits).
    /// </summary>
    public static double TransitOrb(string transitBody) => Moiety(transitBody);
}

public sealed record AspectHit(AspectSpec Aspect, double Deviation, double OrbUsed, bool Applying)
{
    /// <summary>1 at exactness, 0 at the edge of the orb (cosine-shaped, so the peak is broad).</summary>
    public double Strength => Math.Cos(Math.Min(1.0, Deviation / OrbUsed) * Math.PI / 2);
}

public static class AspectFinder
{
    /// <summary>
    /// Closest aspect between a moving point A (with speed in deg/day) and a point B (with its own speed), within
    /// <paramref name="orb"/> degrees for major aspects (× <see cref="AspectCatalog.MinorOrbFactor"/> for minor ones).
    /// </summary>
    public static AspectHit? Find(
        double lonA, double speedA, double lonB, double speedB,
        IReadOnlyList<AspectSpec> specs, double orb, bool majorOnly = false)
    {
        var sep = Zodiac.Separation(lonA, lonB);
        AspectHit? best = null;

        foreach (var spec in specs)
        {
            if (majorOnly && !spec.Major) continue;
            var specOrb = spec.Major ? orb : orb * AspectCatalog.MinorOrbFactor;
            var dev = Math.Abs(sep - spec.Angle);
            if (dev > specOrb) continue;
            if (best is not null && dev / specOrb >= best.Deviation / best.OrbUsed) continue;

            const double dt = 0.01;
            var later = Zodiac.Separation(lonA + speedA * dt, lonB + speedB * dt);
            var applying = Math.Abs(later - spec.Angle) < dev;
            best = new AspectHit(spec, dev, specOrb, applying);
        }

        return best;
    }
}
