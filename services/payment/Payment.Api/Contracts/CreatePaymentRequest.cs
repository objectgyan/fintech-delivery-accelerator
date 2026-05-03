namespace Payment.Api.Contracts;

public sealed record CreatePaymentRequest(
    decimal Amount,
    string Currency,
    string CustomerEmail,
    string PaymentMethodToken,
    string? Description
);
