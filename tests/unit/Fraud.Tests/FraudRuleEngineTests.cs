using FluentAssertions;
using Fraud.Application.Services;
using Fraud.Application.Settings;
using Fraud.Domain.Enums;
using Xunit;

namespace Fraud.Tests;

public sealed class FraudRuleEngineTests
{
    private static FraudRuleEngine CreateEngine(
        decimal highAmountThreshold = 1000m,
        string[]? blockedDomains = null,
        string[]? blockedEmails = null,
        int repeatedAttemptsThreshold = 3) =>
        new(new FraudSettings
        {
            HighAmountThreshold = highAmountThreshold,
            BlockedDomains = blockedDomains?.ToList() ?? [],
            BlockedEmails = blockedEmails?.ToList() ?? [],
            RepeatedAttemptsThreshold = repeatedAttemptsThreshold,
            RepeatedAttemptsWindowMinutes = 10
        });

    [Fact]
    public void Evaluate_CleanPayment_ReturnsApprove()
    {
        var engine = CreateEngine();

        var result = engine.Evaluate(amount: 50m, customerEmail: "user@example.com", recentAttemptCount: 0);

        result.Decision.Should().Be(FraudVerdict.Approve);
        result.Score.Should().Be(0);
        result.RulesTriggered.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_HighAmount_ReturnsReview()
    {
        var engine = CreateEngine(highAmountThreshold: 1000m);

        var result = engine.Evaluate(amount: 1500m, customerEmail: "user@example.com", recentAttemptCount: 0);

        result.Decision.Should().Be(FraudVerdict.Review);
        result.Score.Should().Be(40);
        result.RulesTriggered.Should().ContainSingle().Which.Should().Be("high-amount");
    }

    [Fact]
    public void Evaluate_BlockedDomain_ReturnsDecline()
    {
        var engine = CreateEngine(blockedDomains: ["fraud.net"]);

        var result = engine.Evaluate(amount: 10m, customerEmail: "someone@fraud.net", recentAttemptCount: 0);

        result.Decision.Should().Be(FraudVerdict.Decline);
        result.Score.Should().Be(100);
        result.RulesTriggered.Should().ContainSingle().Which.Should().Be("blocked-domain");
    }

    [Fact]
    public void Evaluate_BlockedEmail_ReturnsDecline()
    {
        var engine = CreateEngine(blockedEmails: ["bad@test.com"]);

        var result = engine.Evaluate(amount: 10m, customerEmail: "bad@test.com", recentAttemptCount: 0);

        result.Decision.Should().Be(FraudVerdict.Decline);
        result.RulesTriggered.Should().Contain("blocked-domain");
    }

    [Fact]
    public void Evaluate_RepeatedAttempts_ReturnsReview()
    {
        var engine = CreateEngine(repeatedAttemptsThreshold: 3);

        var result = engine.Evaluate(amount: 10m, customerEmail: "user@example.com", recentAttemptCount: 3);

        result.Decision.Should().Be(FraudVerdict.Review);
        result.Score.Should().Be(30);
        result.RulesTriggered.Should().ContainSingle().Which.Should().Be("repeated-attempts");
    }

    [Fact]
    public void Evaluate_BelowRepeatedAttemptsThreshold_ReturnsApprove()
    {
        var engine = CreateEngine(repeatedAttemptsThreshold: 3);

        var result = engine.Evaluate(amount: 10m, customerEmail: "user@example.com", recentAttemptCount: 2);

        result.Decision.Should().Be(FraudVerdict.Approve);
        result.RulesTriggered.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_MultipleRules_ScoresAccumulate()
    {
        var engine = CreateEngine(highAmountThreshold: 1000m, repeatedAttemptsThreshold: 3);

        var result = engine.Evaluate(amount: 1500m, customerEmail: "user@example.com", recentAttemptCount: 3);

        result.Decision.Should().Be(FraudVerdict.Review);
        result.Score.Should().Be(70);
        result.RulesTriggered.Should().HaveCount(2)
            .And.Contain("high-amount")
            .And.Contain("repeated-attempts");
    }
}
