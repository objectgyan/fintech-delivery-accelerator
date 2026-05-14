using Fraud.Domain.Entities;
using Fraud.Domain.Repositories;
using Fraud.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fraud.Infrastructure.Repositories;

public sealed class FraudDecisionRepository : IFraudDecisionRepository
{
    private readonly FraudDbContext _context;

    public FraudDecisionRepository(FraudDbContext context) => _context = context;

    public async Task AddAsync(FraudDecision decision, CancellationToken ct = default)
    {
        await _context.FraudDecisions.AddAsync(decision, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> CountRecentByEmailAsync(string email, DateTimeOffset since, CancellationToken ct = default)
    {
        return await _context.FraudDecisions
            .CountAsync(f => f.CustomerEmail == email && f.CreatedAtUtc >= since, ct);
    }
}
