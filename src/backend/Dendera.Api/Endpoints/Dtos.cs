using Dendera.Api.Data;
using Dendera.Api.Services;
using Dendera.Core.Journal;
using Dendera.Core.Readings;

namespace Dendera.Api.Endpoints;

public sealed record ProfileInput(
    string Name,
    DateTime BirthLocal,
    int BirthUtcOffsetMinutes,
    bool BirthTimeKnown,
    string BirthPlace,
    double BirthLatitude,
    double BirthLongitude,
    string? BirthTimeZone,
    string CurrentPlace,
    double CurrentLatitude,
    double CurrentLongitude,
    string? CurrentTimeZone,
    string? HouseSystem,
    string? RuleSetId)
{
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) yield return "name is required";
        if (BirthLatitude is < -90 or > 90 || CurrentLatitude is < -90 or > 90) yield return "latitude out of range";
        if (BirthLongitude is < -180 or > 180 || CurrentLongitude is < -180 or > 180) yield return "longitude out of range";
        if (BirthUtcOffsetMinutes is < -14 * 60 or > 14 * 60) yield return "utc offset out of range";
        if (BirthLocal.Year is < 1700 or > 2200) yield return "birth year out of supported range";
    }

    public void ApplyTo(Profile p)
    {
        p.Name = Name.Trim();
        p.BirthLocal = DateTime.SpecifyKind(BirthLocal, DateTimeKind.Unspecified);
        p.BirthUtcOffsetMinutes = BirthUtcOffsetMinutes;
        p.BirthTimeKnown = BirthTimeKnown;
        p.BirthPlace = BirthPlace;
        p.BirthLatitude = BirthLatitude;
        p.BirthLongitude = BirthLongitude;
        p.BirthTimeZone = BirthTimeZone;
        p.CurrentPlace = CurrentPlace;
        p.CurrentLatitude = CurrentLatitude;
        p.CurrentLongitude = CurrentLongitude;
        p.CurrentTimeZone = CurrentTimeZone;
        p.HouseSystem = string.IsNullOrWhiteSpace(HouseSystem) ? "Placidus" : HouseSystem;
        p.RuleSetId = string.IsNullOrWhiteSpace(RuleSetId) ? "melothesia-classic" : RuleSetId;
    }
}

public sealed record ProfileDto(
    int Id, string Name, DateTime BirthLocal, int BirthUtcOffsetMinutes, DateTimeOffset BirthUtc, bool BirthTimeKnown,
    string BirthPlace, double BirthLatitude, double BirthLongitude, string? BirthTimeZone,
    string CurrentPlace, double CurrentLatitude, double CurrentLongitude, string? CurrentTimeZone,
    string HouseSystem, string RuleSetId)
{
    public static ProfileDto From(Profile p) => new(
        p.Id, p.Name, p.BirthLocal, p.BirthUtcOffsetMinutes, p.BirthUtc, p.BirthTimeKnown,
        p.BirthPlace, p.BirthLatitude, p.BirthLongitude, p.BirthTimeZone,
        p.CurrentPlace, p.CurrentLatitude, p.CurrentLongitude, p.CurrentTimeZone,
        p.HouseSystem, p.RuleSetId);
}

public sealed record ReadingResponse(Reading Reading, IReadOnlyList<SourceStatus> Sources);

public sealed record TimelineResponse(Timeline Timeline, IReadOnlyList<SourceStatus> Sources, int StepHours);

public sealed record JournalDto(
    DateOnly Date, int? Energy, int? Mood, int? Sleep, int? Focus, int? Stress, int? Gut, int? Head, int? Body,
    string? Notes)
{
    public static JournalDto From(JournalDay d) =>
        new(d.Date, d.Energy, d.Mood, d.Sleep, d.Focus, d.Stress, d.Gut, d.Head, d.Body, d.Notes);

    public IEnumerable<string> Validate()
    {
        foreach (var (name, value) in Metrics())
            if (value is < 1 or > 5) yield return $"{name} must be 1..5";
    }

    public void ApplyTo(JournalDay d)
    {
        d.Energy = Energy; d.Mood = Mood; d.Sleep = Sleep; d.Focus = Focus;
        d.Stress = Stress; d.Gut = Gut; d.Head = Head; d.Body = Body;
        d.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
    }

    public IEnumerable<(string name, int? value)> Metrics() =>
    [
        ("energy", Energy), ("mood", Mood), ("sleep", Sleep), ("focus", Focus),
        ("stress", Stress), ("gut", Gut), ("head", Head), ("body", Body),
    ];

    public JournalEntry ToCore() => new(
        Date,
        Metrics().Where(m => m.value.HasValue).ToDictionary(m => m.name, m => m.value!.Value),
        Notes);
}

public sealed record CorrelationResponse(
    DateOnly? From, DateOnly? To, int JournalDays, int TracksTested,
    IReadOnlyList<CorrelationFinding> Findings, IReadOnlyList<SourceStatus> Sources, string? Message);

public sealed record MetricInfo(string Id, string Label);
