using Fraud.Domain.Enums;

namespace Fraud.Application.Services;

public sealed record FraudEvaluation(
    int Score,
    FraudVerdict Decision,
    IReadOnlyList<string> RulesTriggered);
