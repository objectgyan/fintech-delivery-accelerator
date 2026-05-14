using System.Text.Json;
using Fraud.Domain.Enums;

namespace Fraud.Domain.Entities;

public sealed class FraudDecision
{
    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public string CustomerEmail { get; private set; } = string.Empty;
    public int Score { get; private set; }
    public FraudVerdict Decision { get; private set; }
    public string RulesTriggeredJson { get; private set; } = "[]";
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private FraudDecision() { }

    public static FraudDecision Create(
        Guid paymentId,
        string customerEmail,
        int score,
        FraudVerdict decision,
        IReadOnlyList<string> rulesTriggered) => new()
    {
        Id = Guid.NewGuid(),
        PaymentId = paymentId,
        CustomerEmail = customerEmail,
        Score = score,
        Decision = decision,
        RulesTriggeredJson = JsonSerializer.Serialize(rulesTriggered),
        CreatedAtUtc = DateTimeOffset.UtcNow
    };
}
