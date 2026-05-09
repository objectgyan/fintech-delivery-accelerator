using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Payment.Domain.Entities;

namespace Payment.Infrastructure.Data.Configurations;

public sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("PaymentAttempts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.PaymentId).IsRequired();
        builder.Property(a => a.AttemptNo).IsRequired();
        builder.Property(a => a.Provider).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.ErrorCode).HasMaxLength(100);
        builder.Property(a => a.ErrorMessage).HasMaxLength(500);
        builder.Property(a => a.StartedAtUtc).IsRequired();
        builder.Property(a => a.CompletedAtUtc);

        builder.HasIndex(a => a.PaymentId);

        builder.HasOne<Payment.Domain.Entities.Payment>()
               .WithMany()
               .HasForeignKey(a => a.PaymentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
