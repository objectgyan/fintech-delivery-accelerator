namespace Notification.Worker.Data;

public sealed class NotificationLog
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid PaymentId { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public string RecipientEmail { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private NotificationLog() { }

    public static NotificationLog Success(Guid eventId, Guid paymentId, string channel, string recipientEmail) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            PaymentId = paymentId,
            Channel = channel,
            RecipientEmail = recipientEmail,
            Status = "sent",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

    public static NotificationLog Failure(Guid eventId, Guid paymentId, string channel, string recipientEmail, string errorMessage) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            PaymentId = paymentId,
            Channel = channel,
            RecipientEmail = recipientEmail,
            Status = "failed",
            ErrorMessage = errorMessage,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
}
