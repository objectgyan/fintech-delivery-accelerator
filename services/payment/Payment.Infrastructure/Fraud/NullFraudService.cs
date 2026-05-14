using Payment.Application.Fraud;

namespace Payment.Infrastructure.Fraud;

public sealed class NullFraudService : IFraudService
{
    public Task<FraudServiceResult> EvaluateAsync(
        Guid paymentId, decimal amount, string currency, string customerEmail,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new FraudServiceResult(FraudDecision.Approve, 0, []));
}
