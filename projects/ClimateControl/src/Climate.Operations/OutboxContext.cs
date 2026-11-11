using Microsoft.EntityFrameworkCore;

namespace Climate.Operations;

public sealed class OutboxContext(DbContextOptions<OutboxContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> Messages => Set<OutboxMessage>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>().ToTable("Outbox");
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => new { x.SensorId, x.Sequence }).IsUnique();
        modelBuilder.Entity<OutboxMessage>().Property(x => x.Priority).HasDefaultValue(0);
    }
    public static OutboxContext Open(string path) => new(new DbContextOptionsBuilder<OutboxContext>()
        .UseSqlite("Data Source=" + path).Options);
}

public enum DeliveryState { Pending, Delivered, NeedsReview }
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string SensorId { get; set; } = "";
    public long Sequence { get; set; }
    public string Payload { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public int Attempts { get; set; }
    public DeliveryState State { get; set; }
    public int Priority { get; set; }
}
