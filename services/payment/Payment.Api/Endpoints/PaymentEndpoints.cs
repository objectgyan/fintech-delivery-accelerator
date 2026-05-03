using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Payment.Api.Contracts;
using Payment.Application.Commands.CreatePayment;

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
            .ProducesValidationProblem();
    }

    private static async Task<IResult> CreatePayment(
        [FromBody] CreatePaymentRequest request,
        IValidator<CreatePaymentCommand> validator,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new CreatePaymentCommand(
            request.Amount,
            request.Currency,
            request.CustomerEmail,
            request.PaymentMethodToken,
            request.Description);

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

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
}
