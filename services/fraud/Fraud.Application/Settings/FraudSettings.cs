namespace Fraud.Application.Settings;

public sealed class FraudSettings
{
    public decimal HighAmountThreshold { get; init; } = 1000m;
    public List<string> BlockedEmails { get; init; } = [];
    public List<string> BlockedDomains { get; init; } = [];
    public int RepeatedAttemptsThreshold { get; init; } = 3;
    public int RepeatedAttemptsWindowMinutes { get; init; } = 10;
}
