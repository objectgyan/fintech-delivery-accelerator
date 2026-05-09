using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;

namespace Payment.Infrastructure.Data.Configurations;

public sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("IdempotencyKeys");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Key).HasMaxLength(128).IsRequired();
        builder.Property(k => k.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(k => k.PaymentId);
        builder.Property(k => k.ResponseJson);
        builder.Property(k => k.ResponseStatusCode).IsRequired();
        builder.Property(k => k.CreatedAtUtc).IsRequired();
        builder.Property(k => k.CompletedAtUtc);

        builder.HasIndex(k => k.Key)
               .IsUnique()
               .HasDatabaseName("UX_IdempotencyKeys_Key");
    }
}
