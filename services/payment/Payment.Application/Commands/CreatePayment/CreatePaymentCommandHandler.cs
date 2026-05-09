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
    private readonly IEventPublisher _eventPublisher;

    public CreatePaymentCommandHandler(
        IPaymentProvider provider,
        IPaymentRepository repository,
        IEventPublisher eventPublisher)
    {
        _provider = provider;
        _repository = repository;
        _eventPublisher = eventPublisher;
    }

    public async Task<CreatePaymentResult> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = Domain.Entities.Payment.Create(request.Amount, request.Currency, request.CustomerEmail);

        // Save as Pending before calling provider so the FK from PaymentAttempts is valid
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
        await _repository.UpdateAsync(payment, cancellationToken);
        await _repository.AddAttemptAsync(attempt, cancellationToken);

        // TODO: Phase 2 replaces direct publish with outbox (FT-205)
        var evt = PaymentAuthorizedEvent.From(
            paymentId: payment.Id,
            amount: payment.Amount,
            currency: payment.Currency,
            customerEmail: payment.CustomerEmail,
            traceId: request.TraceId ?? string.Empty);

        await _eventPublisher.PublishAsync(evt, cancellationToken);

        return new CreatePaymentResult(
            PaymentId: payment.Id,
            Status: payment.Status.ToString().ToLowerInvariant(),
            Provider: payment.Provider,
            ProviderReference: payment.ProviderReference,
            CreatedAtUtc: payment.CreatedAtUtc
        );
    }
}
