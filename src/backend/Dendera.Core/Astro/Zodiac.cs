namespace Dendera.Core.Astro;

/// <summary>Angle helpers and the tropical zodiac.</summary>
public static class Zodiac
{
    public static readonly IReadOnlyList<string> Signs =
    [
        "aries", "taurus", "gemini", "cancer", "leo", "virgo",
        "libra", "scorpio", "sagittarius", "capricorn", "aquarius", "pisces",
    ];

    public static double Norm(double deg)
    {
        var d = deg % 360.0;
        return d < 0 ? d + 360.0 : d;
    }

    public static string SignOf(double longitude) => Signs[(int)(Norm(longitude) / 30.0) % 12];

    public static double DegreeInSign(double longitude) => Norm(longitude) % 30.0;

    /// <summary>Smallest angle between two longitudes, 0..180.</summary>
    public static double Separation(double a, double b)
    {
        var d = Norm(a - b);
        return d > 180 ? 360 - d : d;
    }

    /// <summary>Signed shortest delta from <paramref name="from"/> to <paramref name="to"/>, -180..180.</summary>
    public static double Delta(double from, double to) => Norm(to - from + 180.0) - 180.0;

    public static string Format(double longitude)
    {
        var deg = DegreeInSign(longitude);
        var d = (int)deg;
        var m = (int)Math.Round((deg - d) * 60);
        if (m == 60) { d++; m = 0; }
        return $"{d}°{m:00}' {Lexicon.Name(SignOf(longitude))}";
    }

    internal const double Rad = Math.PI / 180.0;
}
