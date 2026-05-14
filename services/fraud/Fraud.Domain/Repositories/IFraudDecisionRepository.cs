using Fraud.Domain.Entities;

namespace Fraud.Domain.Repositories;

public interface IFraudDecisionRepository
{
    Task AddAsync(FraudDecision decision, CancellationToken ct = default);
    Task<int> CountRecentByEmailAsync(string email, DateTimeOffset since, CancellationToken ct = default);
}
