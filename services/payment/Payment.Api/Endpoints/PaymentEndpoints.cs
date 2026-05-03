using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Payment.Api.Contracts;
using Payment.Application.Commands.CreatePayment;
using Payment.Application.Exceptions;

namespace Payment.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments").WithTags("Payments");

        group.MapPost("", CreatePayment)
            .WithName("CreatePayment")
            .WithSummary("Initiate a payment")
            .Produces<CreatePaymentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status402PaymentRequired)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> CreatePayment(
        [FromBody] CreatePaymentRequest request,
        IValidator<CreatePaymentCommand> validator,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new CreatePaymentCommand(
            request.Amount,
            request.Currency,
            request.CustomerEmail,
            request.PaymentMethodToken,
            request.Description,
            TraceId: httpContext.TraceIdentifier);

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        try
        {
            var result = await sender.Send(command, cancellationToken);

            return Results.Created(
                $"/payments/{result.PaymentId}",
                new CreatePaymentResponse(
                    result.PaymentId,
                    result.Status,
                    result.Provider,
                    result.ProviderReference,
                    result.CreatedAtUtc));
        }
        catch (PaymentProviderException ex)
        {
            return Results.Problem(
                title: ex.IsTransient ? "Payment provider temporarily unavailable" : "Payment declined",
                detail: ex.Message,
                statusCode: ex.IsTransient ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status402PaymentRequired,
                extensions: new Dictionary<string, object?> { ["code"] = ex.ErrorCode });
        }
    }
}
