namespace Dendera.Core.Astro;

public enum HouseSystem
{
    Placidus,
    WholeSign,
    Porphyry,
}

/// <summary>Chart angles and house cusps. All angles in degrees.</summary>
public static class Houses
{
    public static double Midheaven(double ramc, double obliquity)
    {
        var r = ramc * Zodiac.Rad;
        var e = obliquity * Zodiac.Rad;
        return Zodiac.Norm(Math.Atan2(Math.Sin(r), Math.Cos(r) * Math.Cos(e)) / Zodiac.Rad);
    }

    public static double Ascendant(double ramc, double obliquity, double latitude)
    {
        var r = ramc * Zodiac.Rad;
        var e = obliquity * Zodiac.Rad;
        var f = latitude * Zodiac.Rad;
        var y = Math.Cos(r);
        var x = -(Math.Sin(r) * Math.Cos(e) + Math.Tan(f) * Math.Sin(e));
        return Zodiac.Norm(Math.Atan2(y, x) / Zodiac.Rad);
    }

    /// <summary>Returns 12 cusps (index 0 = 1st house). <paramref name="used"/> reports the actual system (fallbacks).</summary>
    public static double[] Cusps(HouseSystem system, double ramc, double obliquity, double latitude, out HouseSystem used)
    {
        var asc = Ascendant(ramc, obliquity, latitude);
        var mc = Midheaven(ramc, obliquity);
        used = system;

        if (system == HouseSystem.WholeSign)
        {
            var first = Math.Floor(asc / 30.0) * 30.0;
            return Enumerable.Range(0, 12).Select(i => Zodiac.Norm(first + i * 30)).ToArray();
        }

        if (system == HouseSystem.Placidus)
        {
            var placidus = Placidus(ramc, obliquity, latitude, asc, mc);
            if (placidus is not null) return placidus;
            used = HouseSystem.Porphyry;
        }

        return Porphyry(asc, mc);
    }

    /// <summary>House number (1..12) of a longitude given the cusps.</summary>
    public static int HouseOf(IReadOnlyList<double> cusps, double longitude)
    {
        for (var i = 0; i < 12; i++)
        {
            var start = cusps[i];
            var end = cusps[(i + 1) % 12];
            var span = Zodiac.Norm(end - start);
            if (Zodiac.Norm(longitude - start) < span) return i + 1;
        }
        return 1;
    }

    private static double[] Porphyry(double asc, double mc)
    {
        var ic = Zodiac.Norm(mc + 180);
        var c = new double[12];
        c[0] = asc;
        c[3] = ic;
        c[6] = Zodiac.Norm(asc + 180);
        c[9] = mc;
        var q1 = Zodiac.Norm(ic - asc);
        c[1] = Zodiac.Norm(asc + q1 / 3);
        c[2] = Zodiac.Norm(asc + 2 * q1 / 3);
        var q2 = Zodiac.Norm(c[6] - ic);
        c[4] = Zodiac.Norm(ic + q2 / 3);
        c[5] = Zodiac.Norm(ic + 2 * q2 / 3);
        c[7] = Zodiac.Norm(c[1] + 180);
        c[8] = Zodiac.Norm(c[2] + 180);
        c[10] = Zodiac.Norm(c[4] + 180);
        c[11] = Zodiac.Norm(c[5] + 180);
        return c;
    }

    /// <summary>Placidus by semi-arc iteration. Returns null where it is undefined (polar latitudes).</summary>
    private static double[]? Placidus(double ramc, double obliquity, double latitude, double asc, double mc)
    {
        if (Math.Abs(latitude) >= 66.0) return null;

        var c11 = PlacidusCusp(ramc, obliquity, latitude, 1.0 / 3.0, above: true);
        var c12 = PlacidusCusp(ramc, obliquity, latitude, 2.0 / 3.0, above: true);
        var c2 = PlacidusCusp(ramc, obliquity, latitude, 2.0 / 3.0, above: false);
        var c3 = PlacidusCusp(ramc, obliquity, latitude, 1.0 / 3.0, above: false);
        if (c11 is null || c12 is null || c2 is null || c3 is null) return null;

        var c = new double[12];
        c[0] = asc;
        c[1] = c2.Value;
        c[2] = c3.Value;
        c[3] = Zodiac.Norm(mc + 180);
        c[4] = Zodiac.Norm(c11.Value + 180);
        c[5] = Zodiac.Norm(c12.Value + 180);
        c[6] = Zodiac.Norm(asc + 180);
        c[7] = Zodiac.Norm(c2.Value + 180);
        c[8] = Zodiac.Norm(c3.Value + 180);
        c[9] = mc;
        c[10] = c11.Value;
        c[11] = c12.Value;
        return c;
    }

    /// <param name="fraction">Fraction of the semi-arc (1/3 or 2/3).</param>
    /// <param name="above">True for houses 11/12 (diurnal arc from MC), false for 2/3 (nocturnal arc back from IC).</param>
    private static double? PlacidusCusp(double ramc, double obliquity, double latitude, double fraction, bool above)
    {
        var e = obliquity * Zodiac.Rad;
        var tanPhi = Math.Tan(latitude * Zodiac.Rad);
        var ra = above ? ramc + fraction * 90 : ramc + 180 - fraction * 90;
        var lon = 0.0;

        for (var i = 0; i < 100; i++)
        {
            lon = EclipticFromRa(ra, e);
            var decl = Math.Asin(Math.Sin(e) * Math.Sin(lon * Zodiac.Rad));
            var x = -tanPhi * Math.Tan(decl);
            if (x is < -1 or > 1) return null;
            var sda = Math.Acos(x) / Zodiac.Rad;
            var next = above ? ramc + fraction * sda : ramc + 180 - fraction * (180 - sda);
            if (Math.Abs(Zodiac.Delta(ra, next)) < 1e-9) { ra = next; break; }
            ra = next;
        }

        return EclipticFromRa(ra, e);
    }

    /// <summary>Ecliptic longitude of the ecliptic point with the given right ascension.</summary>
    private static double EclipticFromRa(double raDeg, double obliquityRad)
    {
        var a = raDeg * Zodiac.Rad;
        return Zodiac.Norm(Math.Atan2(Math.Sin(a), Math.Cos(a) * Math.Cos(obliquityRad)) / Zodiac.Rad);
    }
}
