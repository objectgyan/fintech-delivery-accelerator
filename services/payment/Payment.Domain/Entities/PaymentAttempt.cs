namespace Payment.Domain.Entities;

public sealed class PaymentAttempt
{
    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public int AttemptNo { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public AttemptStatus Status { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    private PaymentAttempt() { }

    public static PaymentAttempt Start(Guid paymentId, int attemptNo, string provider) => new()
    {
        Id = Guid.NewGuid(),
        PaymentId = paymentId,
        AttemptNo = attemptNo,
        Provider = provider,
        Status = AttemptStatus.InProgress,
        StartedAtUtc = DateTimeOffset.UtcNow
    };

    public void Succeed()
    {
        Status = AttemptStatus.Succeeded;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Fail(string? errorCode, string? errorMessage, bool isTransient)
    {
        Status = isTransient ? AttemptStatus.TransientError : AttemptStatus.Failed;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }
}

public enum AttemptStatus { InProgress, Succeeded, Failed, TransientError }
