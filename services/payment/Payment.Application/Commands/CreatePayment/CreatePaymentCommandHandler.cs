using System.Text.Json;
using MediatR;
using Payment.Application.Events;
using Payment.Application.Exceptions;
using Payment.Application.Providers;
using Payment.Domain.Entities;
using Payment.Domain.Repositories;

namespace Payment.Application.Commands.CreatePayment;

public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, CreatePaymentResult>
{
    private readonly IPaymentProvider _provider;
    private readonly IPaymentRepository _repository;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public CreatePaymentCommandHandler(IPaymentProvider provider, IPaymentRepository repository)
    {
        _provider = provider;
        _repository = repository;
    }

    public async Task<CreatePaymentResult> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = Domain.Entities.Payment.Create(request.Amount, request.Currency, request.CustomerEmail);

        // Save as Pending first so PaymentAttempts FK is valid
        await _repository.AddAsync(payment, cancellationToken);

        var attempt = PaymentAttempt.Start(payment.Id, attemptNo: 1, provider: "stripe");

        var providerResult = await _provider.AuthorizeAsync(
            request.Amount,
            request.Currency,
            request.PaymentMethodToken,
            cancellationToken);

        if (!providerResult.Success)
        {
            payment.Fail();
            attempt.Fail(providerResult.ErrorCode, providerResult.ErrorMessage, providerResult.IsTransient);
            await _repository.UpdateAsync(payment, cancellationToken);
            await _repository.AddAttemptAsync(attempt, cancellationToken);
            throw new PaymentProviderException(providerResult.ErrorCode, providerResult.ErrorMessage, providerResult.IsTransient);
        }

        payment.Authorize(providerResult.ProviderReference!);
        attempt.Succeed();

        var evt = PaymentAuthorizedEvent.From(
            paymentId: payment.Id,
            amount: payment.Amount,
            currency: payment.Currency,
            customerEmail: payment.CustomerEmail,
            traceId: request.TraceId ?? string.Empty);

        var outboxMessage = OutboxMessage.Create(
            eventType: evt.EventType,
            payloadJson: JsonSerializer.Serialize(evt, JsonOptions));

        var result = new CreatePaymentResult(
            PaymentId: payment.Id,
            Status: payment.Status.ToString().ToLowerInvariant(),
            Provider: payment.Provider,
            ProviderReference: payment.ProviderReference,
            CreatedAtUtc: payment.CreatedAtUtc);

        if (request.IdempotencyKeyEntity is { } idempotencyKey)
        {
            idempotencyKey.Complete(
                paymentId: payment.Id,
                responseJson: JsonSerializer.Serialize(result, JsonOptions),
                statusCode: 201);
            // Payment + attempt + outbox + idempotency key committed in one transaction
            await _repository.CompleteWithIdempotencyAsync(payment, attempt, outboxMessage, idempotencyKey, cancellationToken);
        }
        else
        {
            await _repository.CompletePaymentAsync(payment, attempt, outboxMessage, cancellationToken);
        }

        return result;
    }
}
