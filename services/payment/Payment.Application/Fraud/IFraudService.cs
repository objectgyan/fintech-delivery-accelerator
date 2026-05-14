namespace Payment.Application.Fraud;

public interface IFraudService
{
    Task<FraudServiceResult> EvaluateAsync(
        Guid paymentId,
        decimal amount,
        string currency,
        string customerEmail,
        CancellationToken cancellationToken = default);
}
