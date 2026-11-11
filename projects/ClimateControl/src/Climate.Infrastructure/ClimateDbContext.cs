using Microsoft.EntityFrameworkCore;

namespace Climate.Infrastructure;

/// <summary>Relational telemetry storage; SQLite is used for the educational stand.</summary>
public sealed class ClimateDbContext(DbContextOptions<ClimateDbContext> options) : DbContext(options)
{
    public DbSet<TelemetryRow> Telemetry => Set<TelemetryRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TelemetryRow>().HasIndex(x => new { x.SensorId, x.Sequence }).IsUnique();
        modelBuilder.Entity<TelemetryRow>().Property(x => x.SensorId).HasMaxLength(7);
        modelBuilder.Entity<TelemetryRow>().Property(x => x.RoomId).HasMaxLength(4);
    }
}

/// <summary>Accepted measurements; room is assigned from the device registry.</summary>
public sealed class TelemetryRow
{
    public long Id { get; set; }
    public string SensorId { get; set; } = "";
    public string RoomId { get; set; } = "";
    public long Sequence { get; set; }
    public decimal TemperatureC { get; set; }
    public decimal HumidityPercent { get; set; }
    public DateTimeOffset MeasuredAt { get; set; }
}
