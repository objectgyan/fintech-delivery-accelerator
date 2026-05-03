namespace Payment.Domain.Entities;

public sealed class Payment
{
    public Guid Id { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string CustomerEmail { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string? ProviderReference { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private Payment() { } // required by EF Core

    public static Payment Create(decimal amount, string currency, string customerEmail)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerEmail);

        var now = DateTimeOffset.UtcNow;
        return new Payment
        {
            Id = Guid.NewGuid(),
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            CustomerEmail = customerEmail,
            Status = PaymentStatus.Pending,
            Provider = "stripe",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Authorize(string providerReference)
    {
        ProviderReference = providerReference;
        Status = PaymentStatus.Authorized;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Fail()
    {
        Status = PaymentStatus.Failed;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

public enum PaymentStatus { Pending, Authorized, Captured, Failed }
