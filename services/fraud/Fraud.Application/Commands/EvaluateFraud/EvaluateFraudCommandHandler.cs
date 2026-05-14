using Fraud.Application.Services;
using Fraud.Application.Settings;
using Fraud.Domain.Entities;
using Fraud.Domain.Repositories;
using MediatR;

namespace Fraud.Application.Commands.EvaluateFraud;

public sealed class EvaluateFraudCommandHandler : IRequestHandler<EvaluateFraudCommand, FraudDecisionResult>
{
    private readonly FraudRuleEngine _engine;
    private readonly IFraudDecisionRepository _repository;
    private readonly FraudSettings _settings;

    public EvaluateFraudCommandHandler(
        FraudRuleEngine engine,
        IFraudDecisionRepository repository,
        FraudSettings settings)
    {
        _engine = engine;
        _repository = repository;
        _settings = settings;
    }

    public async Task<FraudDecisionResult> Handle(EvaluateFraudCommand command, CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.AddMinutes(-_settings.RepeatedAttemptsWindowMinutes);
        var recentCount = await _repository.CountRecentByEmailAsync(command.CustomerEmail, since, cancellationToken);

        var evaluation = _engine.Evaluate(command.Amount, command.CustomerEmail, recentCount);

        var decision = FraudDecision.Create(
            command.PaymentId,
            command.CustomerEmail,
            evaluation.Score,
            evaluation.Decision,
            evaluation.RulesTriggered);

        await _repository.AddAsync(decision, cancellationToken);

        return new FraudDecisionResult(
            decision.Id,
            decision.PaymentId,
            decision.Score,
            decision.Decision,
            evaluation.RulesTriggered,
            decision.CreatedAtUtc);
    }
}
