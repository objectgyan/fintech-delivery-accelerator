namespace Payment.Application.Commands.CreatePayment;

public sealed record CreatePaymentResult(
    Guid PaymentId,
    string Status,
    string Provider,
    string? ProviderReference,
    DateTimeOffset CreatedAtUtc,
    string? FraudDecision = null,
    int? FraudScore = null);
