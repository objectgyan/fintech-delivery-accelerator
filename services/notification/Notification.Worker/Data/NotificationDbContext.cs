using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Notification.Worker.Data;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
    }
}

internal sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("NotificationLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Channel).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RecipientEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);

        // Unique index prevents duplicate sends for the same event+channel
        builder.HasIndex(x => new { x.EventId, x.Channel }).IsUnique()
            .HasDatabaseName("IX_NotificationLogs_EventId_Channel");

        builder.HasIndex(x => x.PaymentId)
            .HasDatabaseName("IX_NotificationLogs_PaymentId");
    }
}
