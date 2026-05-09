namespace Payment.Domain.Entities;

public sealed class IdempotencyKey
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public Guid? PaymentId { get; private set; }
    public string? ResponseJson { get; private set; }
    public int ResponseStatusCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    private IdempotencyKey() { }

    public static IdempotencyKey Create(string key, string requestHash) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        RequestHash = requestHash,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    public void Complete(Guid paymentId, string responseJson, int statusCode)
    {
        PaymentId = paymentId;
        ResponseJson = responseJson;
        ResponseStatusCode = statusCode;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }
}
