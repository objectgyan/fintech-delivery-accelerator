namespace Payment.Api.Contracts;

public sealed record CreatePaymentResponse(
    Guid PaymentId,
    string Status,
    string Provider,
    string? ProviderReference,
    DateTimeOffset CreatedAtUtc,
    string? FraudDecision = null,
    int? FraudScore = null
);
