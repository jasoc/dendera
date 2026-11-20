using Microsoft.EntityFrameworkCore;

namespace Dendera.Api.Data;

public sealed class DenderaDb(DbContextOptions<DenderaDb> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<JournalDay> Journal => Set<JournalDay>();

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
    public DateTimeOffset UpdatedAt { get; set; }
}
