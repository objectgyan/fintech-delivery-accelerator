namespace Payment.Application.Fraud;

public sealed record FraudServiceResult(
    string Decision,
    int Score,
    IReadOnlyList<string> RulesTriggered);

public static class FraudDecision
{
    public const string Approve = "approve";
    public const string Review = "review";
    public const string Decline = "decline";
}
