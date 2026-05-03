namespace Notification.Worker.Events;

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
);
