using Fraud.Application.Settings;
using Fraud.Domain.Enums;

namespace Fraud.Application.Services;

public sealed class FraudRuleEngine
{
    private readonly FraudSettings _settings;

    public FraudRuleEngine(FraudSettings settings) => _settings = settings;

    public FraudEvaluation Evaluate(decimal amount, string customerEmail, int recentAttemptCount)
    {
        var rulesTriggered = new List<string>();
        var score = 0;

        // Rule 1: High amount
        if (amount > _settings.HighAmountThreshold)
        {
            rulesTriggered.Add("high-amount");
            score += 40;
        }

        // Rule 2: Blocked email or domain
        var domain = customerEmail.Contains('@') ? customerEmail.Split('@')[1] : string.Empty;
        if (_settings.BlockedEmails.Contains(customerEmail, StringComparer.OrdinalIgnoreCase)
            || _settings.BlockedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            rulesTriggered.Add("blocked-domain");
            score += 100;
        }

        // Rule 3: Repeated attempts from same email in window
        if (recentAttemptCount >= _settings.RepeatedAttemptsThreshold)
        {
            rulesTriggered.Add("repeated-attempts");
            score += 30;
        }

        var decision = score >= 100 ? FraudVerdict.Decline
                     : score >= 30  ? FraudVerdict.Review
                     : FraudVerdict.Approve;

        return new FraudEvaluation(score, decision, rulesTriggered);
    }
}
