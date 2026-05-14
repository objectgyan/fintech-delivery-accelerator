using MediatR;

namespace Fraud.Application.Commands.EvaluateFraud;

public sealed record EvaluateFraudCommand(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string CustomerEmail) : IRequest<FraudDecisionResult>;
