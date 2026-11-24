using Dendera.Core.Astro;
using Dendera.Core.Signals;

namespace Dendera.Core.Tests;

public class TraditionalTimeTests
{
    private static readonly AstronomyEngineEphemeris Eph = new();

    [Fact]
    public void Planetary_hours_start_at_sunrise_with_the_ruler_of_the_day()
    {
        // Sunday 22 Nov 2026 in Rome: sunrise ~05:57 UTC; the first hour is the Sun's, the second Venus'.
        var first = PlanetaryHours.At(Eph, new DateTimeOffset(2026, 11, 22, 6, 10, 0, TimeSpan.Zero), 41.9, 12.5)!;
        Assert.Equal(Bodies.Sun, first.DayRuler);
        Assert.Equal(1, first.Number);
        Assert.Equal(Bodies.Sun, first.Ruler);
        Assert.InRange(first.Start.UtcDateTime.TimeOfDay.TotalMinutes, 5 * 60 + 45, 6 * 60 + 10);

        var second = PlanetaryHours.At(Eph, first.End.AddMinutes(5), 41.9, 12.5)!;
        Assert.Equal(2, second.Number);
        Assert.Equal(Bodies.Venus, second.Ruler);

        // The night before Monday's sunrise still belongs to Sunday.
        var night = PlanetaryHours.At(Eph, new DateTimeOffset(2026, 11, 22, 23, 0, 0, TimeSpan.Zero), 41.9, 12.5)!;
        Assert.False(night.Day);
        Assert.Equal(Bodies.Sun, night.DayRuler);
        Assert.InRange(night.Number, 13, 24);

        Assert.Null(PlanetaryHours.At(Eph, new DateTimeOffset(2026, 12, 21, 12, 0, 0, TimeSpan.Zero), 80, 15)); // polar night
    }

    [Fact]
    public void Ganzhi_day_and_year_match_known_dates()
    {
        // 1 Jan 2000 was a wuwu day (54) in the year jimao (1999, before Lichun).
        var at = new DateTimeOffset(2000, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var p = TcmCalendar.PillarsAt(at, 0, Eph.Position(Bodies.Sun, at).Longitude);
        Assert.Equal(54, p.Day);
        Assert.Equal(15, p.Year); // 1999 = jimao
        // 1984 after Lichun was jiazi.
        var y84 = new DateTimeOffset(1984, 6, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(0, TcmCalendar.PillarsAt(y84, 0, Eph.Position(Bodies.Sun, y84).Longitude).Year);
    }

    [Fact]
    public void Vimshottari_starts_with_the_lord_of_the_birth_nakshatra_and_spans_120_years()
    {
        Assert.Equal(120, LunarStations.DashaYears.Values.Sum());
        var natal = ChartCalculator.Compute(Eph, new BirthData(new DateTimeOffset(2001, 5, 6, 10, 0, 0, TimeSpan.Zero), 37.5, 15.07));
        var moon = LunarStations.Sidereal(natal.Point(Bodies.Moon)!.Longitude, natal.Birth.BirthUtc);
        var lord = LunarStations.DashaLords[LunarStations.NakshatraOf(moon).Index % 9];
        var atBirth = LifeTimes.DashaAt(natal, natal.Birth.BirthUtc)!;
        Assert.Equal(lord, atBirth.Lord);
        Assert.True(atBirth.Start <= natal.Birth.BirthUtc);
        Assert.NotNull(LifeTimes.DashaAt(natal, natal.Birth.BirthUtc.AddYears(100)));
    }

    [Fact]
    public void Profection_moves_one_sign_a_year_from_the_ascendant()
    {
        var natal = ChartCalculator.Compute(Eph, new BirthData(new DateTimeOffset(2001, 5, 6, 10, 0, 0, TimeSpan.Zero), 37.5, 15.07));
        var asc = natal.Point(Bodies.Ascendant)!.Sign;
        var p0 = LifeTimes.ProfectionAt(natal, natal.Birth.BirthUtc.AddDays(10));
        Assert.Equal(asc, p0.Sign);
        Assert.Equal(1, p0.House);
        var p25 = LifeTimes.ProfectionAt(natal, natal.Birth.BirthUtc.AddDays(25.5 * 365.25));
        Assert.Equal(2, p25.House); // 25 % 12 = 1 → second house
        Assert.Equal(LifeTimes.Rulers[p25.Sign], p25.Lord);
    }

    [Fact]
    public void Decans_follow_the_chaldean_faces_from_mars()
    {
        var gen = new SkySignalGenerator(Eph);
        var signals = gen.At(new DateTimeOffset(2026, 3, 22, 12, 0, 0, TimeSpan.Zero), new SkyContext(45, 9, null));
        var sun = signals.Single(s => s.Kind == SignalKinds.Decan && s.Tag("body") == Bodies.Sun);
        Assert.Equal("aries", sun.Tag("sign"));
        Assert.Equal("1", sun.Tag("decan"));
        Assert.Equal(Bodies.Mars, sun.Tag("face"));
    }
}
