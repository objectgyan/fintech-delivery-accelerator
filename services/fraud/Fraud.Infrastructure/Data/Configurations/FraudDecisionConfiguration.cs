using Fraud.Domain.Entities;
using Fraud.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fraud.Infrastructure.Data.Configurations;

public sealed class FraudDecisionConfiguration : IEntityTypeConfiguration<FraudDecision>
{
    public void Configure(EntityTypeBuilder<FraudDecision> builder)
    {
        builder.ToTable("FraudDecisions");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.PaymentId).IsRequired();
        builder.Property(f => f.CustomerEmail).HasMaxLength(256).IsRequired();
        builder.Property(f => f.Score).IsRequired();
        builder.Property(f => f.Decision)
               .HasConversion(v => v.ToString(), v => Enum.Parse<FraudVerdict>(v))
               .HasMaxLength(20).IsRequired();
        builder.Property(f => f.RulesTriggeredJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(f => f.CreatedAtUtc).IsRequired();

        builder.HasIndex(f => f.PaymentId).HasDatabaseName("IX_FraudDecisions_PaymentId");
        builder.HasIndex(f => new { f.CustomerEmail, f.CreatedAtUtc })
               .HasDatabaseName("IX_FraudDecisions_CustomerEmail_CreatedAtUtc");
    }
}
