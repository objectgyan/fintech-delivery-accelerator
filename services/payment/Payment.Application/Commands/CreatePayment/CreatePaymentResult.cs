namespace Payment.Application.Commands.CreatePayment;

public sealed record CreatePaymentResult(
    Guid PaymentId,
    string Status,
    string Provider,
    string? ProviderReference,
    DateTimeOffset CreatedAtUtc
);
