using Fraud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fraud.Infrastructure.Data;

public sealed class FraudDbContext : DbContext
{
    public FraudDbContext(DbContextOptions<FraudDbContext> options) : base(options) { }

    public DbSet<FraudDecision> FraudDecisions => Set<FraudDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FraudDbContext).Assembly);
    }
}
