using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Payment.Api.Contracts;
using Payment.Application.Commands.CreatePayment;
using Payment.Application.Exceptions;
using Payment.Domain.Entities;
using Payment.Domain.Repositories;
using FraudDeclinedException = Payment.Application.Exceptions.FraudDeclinedException;

namespace Payment.Api.Endpoints;

public static class PaymentEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments").WithTags("Payments");

        group.MapPost("", CreatePayment)
            .WithName("CreatePayment")
            .WithSummary("Initiate a payment")
            .Produces<CreatePaymentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status402PaymentRequired)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> CreatePayment(
        [FromBody] CreatePaymentRequest request,
        IValidator<CreatePaymentCommand> validator,
        ISender sender,
        IPaymentRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyHeader)
            || string.IsNullOrWhiteSpace(idempotencyKeyHeader))
        {
            return Results.Problem(
                title: "Idempotency-Key header is required",
                detail: "Provide a unique Idempotency-Key header (1-128 printable ASCII characters) with each POST /payments request.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var idempotencyKey = idempotencyKeyHeader.ToString();
        if (idempotencyKey.Length > 128 || idempotencyKey.Any(c => c < 32 || c > 126))
        {
            return Results.Problem(
                title: "Invalid Idempotency-Key format",
                detail: "Idempotency-Key must be 1-128 printable ASCII characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestJson))).ToLowerInvariant();

        // Idempotency dedup check
        var existing = await repository.GetIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
            {
                return Results.Problem(
                    title: "Idempotency key reuse with different request body",
                    detail: "The provided Idempotency-Key was already used with a different request payload.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            // Same key + same hash — return cached response
            if (existing.ResponseJson is not null)
            {
                var cached = JsonSerializer.Deserialize<CreatePaymentResponse>(existing.ResponseJson, JsonOptions);
                return Results.Created($"/payments/{cached!.PaymentId}", cached);
            }

            // Key exists but not yet completed (concurrent request) — treat as conflict
            return Results.Problem(
                title: "Payment already in progress",
                detail: "A payment with this Idempotency-Key is already being processed.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Reserve the key before processing so concurrent duplicates get 409
        var idempotencyKeyEntity = IdempotencyKey.Create(idempotencyKey, requestHash);
        try
        {
            await repository.AddIdempotencyKeyAsync(idempotencyKeyEntity, cancellationToken);
        }
        catch (IdempotencyConflictException)
        {
            return Results.Problem(
                title: "Payment already in progress",
                detail: "A payment with this Idempotency-Key is already being processed.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var command = new CreatePaymentCommand(
            request.Amount,
            request.Currency,
            request.CustomerEmail,
            request.PaymentMethodToken,
            request.Description,
            TraceId: httpContext.TraceIdentifier,
            IdempotencyKey: idempotencyKey,
            RequestHash: requestHash,
            IdempotencyKeyEntity: idempotencyKeyEntity);

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
                    result.CreatedAtUtc,
                    result.FraudDecision,
                    result.FraudScore));
        }
        catch (FraudDeclinedException ex)
        {
            return Results.Problem(
                title: "Payment declined by fraud screening",
                detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "fraud_decline",
                    ["score"] = ex.Score,
                    ["rulesTriggered"] = ex.RulesTriggered
                });
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
