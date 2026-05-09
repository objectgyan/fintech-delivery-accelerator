using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;

namespace Payment.Infrastructure.Data.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.EventType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.PayloadJson).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.RetryCount).IsRequired();
        builder.Property(m => m.CreatedAtUtc).IsRequired();
        builder.Property(m => m.ProcessedAtUtc);

        // Filtered index — relay query only scans Pending rows
        builder.HasIndex(m => new { m.Status, m.CreatedAtUtc })
               .HasFilter("[Status] = 'Pending'")
               .HasDatabaseName("IX_OutboxMessages_Status_CreatedAtUtc");
    }
}
