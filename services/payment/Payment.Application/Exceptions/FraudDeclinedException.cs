namespace Payment.Application.Exceptions;

public sealed class FraudDeclinedException : Exception
{
    public int Score { get; }
    public IReadOnlyList<string> RulesTriggered { get; }

    public FraudDeclinedException(int score, IReadOnlyList<string> rulesTriggered)
        : base("Payment declined by fraud screening.")
    {
        Score = score;
        RulesTriggered = rulesTriggered;
    }
}
