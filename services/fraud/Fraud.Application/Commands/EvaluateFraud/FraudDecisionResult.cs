using Fraud.Domain.Enums;

namespace Fraud.Application.Commands.EvaluateFraud;

public sealed record FraudDecisionResult(
    Guid DecisionId,
    Guid PaymentId,
    int Score,
    FraudVerdict Decision,
    IReadOnlyList<string> RulesTriggered,
    DateTimeOffset EvaluatedAtUtc);
