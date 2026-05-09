namespace Payment.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public OutboxStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(string eventType, string payloadJson) => new()
    {
        Id = Guid.NewGuid(),
        EventType = eventType,
        PayloadJson = payloadJson,
        Status = OutboxStatus.Pending,
        RetryCount = 0,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    public void MarkPublished()
    {
        Status = OutboxStatus.Published;
        ProcessedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkFailed()
    {
        Status = OutboxStatus.Failed;
        ProcessedAtUtc = DateTimeOffset.UtcNow;
    }

    public void IncrementRetry() => RetryCount++;
}

public enum OutboxStatus { Pending, Published, Failed }
