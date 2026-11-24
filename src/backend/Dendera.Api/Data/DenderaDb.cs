using Microsoft.EntityFrameworkCore;

namespace Dendera.Api.Data;

public sealed class DenderaDb(DbContextOptions<DenderaDb> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<JournalDay> Journal => Set<JournalDay>();
    public DbSet<StoredSample> Samples => Set<StoredSample>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Profile>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.BirthPlace).HasMaxLength(300);
            e.Property(p => p.CurrentPlace).HasMaxLength(300);
            e.Property(p => p.HouseSystem).HasMaxLength(20);
            e.Property(p => p.Schools).HasMaxLength(500);
        });

        b.Entity<JournalDay>(e =>
        {
            e.HasIndex(j => new { j.ProfileId, j.Date }).IsUnique();
            e.HasOne<Profile>().WithMany().HasForeignKey(j => j.ProfileId).OnDelete(DeleteBehavior.Cascade);
            e.Property(j => j.Notes).HasMaxLength(4000);
        });

        b.Entity<StoredSample>(e =>
        {
            e.HasIndex(s => new { s.Source, s.Key, s.At }).IsUnique();
            e.HasIndex(s => new { s.Source, s.At });
            e.Property(s => s.Source).HasMaxLength(50);
            e.Property(s => s.Key).HasMaxLength(100);
            e.Property(s => s.Kind).HasMaxLength(50);
            e.Property(s => s.Unit).HasMaxLength(30);
            e.Property(s => s.Label).HasMaxLength(200);
            e.Property(s => s.Detail).HasMaxLength(500);
            e.Property(s => s.TagsJson).HasMaxLength(2000);
        });
    }
}

public sealed class Profile
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Birth wall-clock time at the birth place.</summary>
    public DateTime BirthLocal { get; set; }
    /// <summary>UTC offset in effect at the birth moment (minutes). Explicit because historical DST data can be wrong.</summary>
    public int BirthUtcOffsetMinutes { get; set; }
    public bool BirthTimeKnown { get; set; } = true;
    public string BirthPlace { get; set; } = "";
    public double BirthLatitude { get; set; }
    public double BirthLongitude { get; set; }
    public string? BirthTimeZone { get; set; }

    /// <summary>Where the person lives now (photoperiod, weather).</summary>
    public string CurrentPlace { get; set; } = "";
    public double CurrentLatitude { get; set; }
    public double CurrentLongitude { get; set; }
    public string? CurrentTimeZone { get; set; }

    public string HouseSystem { get; set; } = "Placidus";
    /// <summary>Active schools (rule set ids), comma-separated. Stored so journal correlations stay reproducible.</summary>
    public string Schools { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }

    public IReadOnlyList<string> SchoolIds =>
        Schools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public DateTimeOffset BirthUtc => new DateTimeOffset(
        DateTime.SpecifyKind(BirthLocal, DateTimeKind.Unspecified),
        TimeSpan.FromMinutes(BirthUtcOffsetMinutes)).ToUniversalTime();
}

/// <summary>One journal day. Metrics are 1..5, null when not recorded.</summary>
public sealed class JournalDay
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public DateOnly Date { get; set; }
    public int? Energy { get; set; }
    public int? Mood { get; set; }
    public int? Sleep { get; set; }
    public int? Focus { get; set; }
    public int? Stress { get; set; }
    public int? Gut { get; set; }
    public int? Head { get; set; }
    public int? Body { get; set; }
    public string? Notes { get; set; }
    /// <summary>Local time a malaise began that day (decumbiture, Culpeper): the onset of an illness episode.</summary>
    public TimeOnly? IllnessOnset { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// A signal sample kept from a source whose feed has a short memory (Schumann 24 h, NOAA solar wind 24 h), so history
/// builds up for the timeline, the correlations and, one day, the calibration ritual. Global sources only (no place).
/// </summary>
public sealed class StoredSample
{
    public long Id { get; set; }
    public string Source { get; set; } = "";
    public string Key { get; set; } = "";
    public string Kind { get; set; } = "";
    /// <summary>UTC instant, stored as Unix seconds so SQLite orders and filters it correctly.</summary>
    public long At { get; set; }
    public double Intensity { get; set; }
    public double? Value { get; set; }
    public string? Unit { get; set; }
    public string TagsJson { get; set; } = "{}";
    public string Label { get; set; } = "";
    public string Detail { get; set; } = "";
}
