namespace Payment.Application.Events;

public sealed record PaymentAuthorizedEvent(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string TraceId,
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string CustomerEmail,
    string Status
)
{
    public static PaymentAuthorizedEvent From(
        Guid paymentId,
        decimal amount,
        string currency,
        string customerEmail,
        string traceId)
        => new(
            EventId: Guid.NewGuid(),
            EventType: "PaymentAuthorized.v1",
            OccurredAtUtc: DateTimeOffset.UtcNow,
            TraceId: traceId,
            PaymentId: paymentId,
            Amount: amount,
            Currency: currency,
            CustomerEmail: customerEmail,
            Status: "authorized"
        );
}
