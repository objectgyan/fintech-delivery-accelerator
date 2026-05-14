using Fraud.Application.Commands.EvaluateFraud;
using MediatR;

namespace Fraud.Api.Endpoints;

public static class FraudEndpoints
{
    public static void MapFraudEndpoints(this WebApplication app)
    {
        app.MapPost("/fraud/evaluate", async (EvaluateFraudRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var command = new EvaluateFraudCommand(
                request.PaymentId,
                request.Amount,
                request.Currency,
                request.CustomerEmail);

            var result = await mediator.Send(command, ct);

            return Results.Ok(new
            {
                decisionId = result.DecisionId,
                paymentId = result.PaymentId,
                score = result.Score,
                decision = result.Decision.ToString().ToLowerInvariant(),
                rulesTriggered = result.RulesTriggered,
                evaluatedAtUtc = result.EvaluatedAtUtc
            });
        })
        .WithName("EvaluateFraud");
    }
}

public sealed record EvaluateFraudRequest(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string CustomerEmail);
