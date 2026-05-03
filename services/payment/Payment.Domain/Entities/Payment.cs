namespace Payment.Domain.Entities;

public sealed class Payment
{
    public Guid Id { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Payment() { }

    public static Payment Create(decimal amount, string currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        return new Payment
        {
            Id = Guid.NewGuid(),
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            Status = PaymentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}

public enum PaymentStatus { Pending, Authorized, Captured, Failed }
