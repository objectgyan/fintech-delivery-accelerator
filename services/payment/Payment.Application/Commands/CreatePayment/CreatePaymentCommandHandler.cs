using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Payment.Application.Events;
using Payment.Application.Exceptions;
using Payment.Application.Fraud;
using Payment.Application.Providers;
using Payment.Domain.Entities;
using Payment.Domain.Repositories;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Payment.Application.Commands.CreatePayment;

public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, CreatePaymentResult>
{
    private readonly IPaymentProvider _provider;
    private readonly IFraudService _fraudService;
    private readonly IPaymentRepository _repository;
    private readonly ILogger<CreatePaymentCommandHandler> _logger;
    private readonly ResiliencePipeline<ProviderResult> _circuitBreaker;

    private const int MaxProviderRetries = 3;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public CreatePaymentCommandHandler(
        IPaymentProvider provider,
        IFraudService fraudService,
        IPaymentRepository repository,
        ILogger<CreatePaymentCommandHandler> logger,
        ResiliencePipeline<ProviderResult> circuitBreaker)
    {
        _provider = provider;
        _fraudService = fraudService;
        _repository = repository;
        _logger = logger;
        _circuitBreaker = circuitBreaker;
    }

    public async Task<CreatePaymentResult> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = Domain.Entities.Payment.Create(request.Amount, request.Currency, request.CustomerEmail);

        // Save as Pending first so PaymentAttempts FK is valid
        await _repository.AddAsync(payment, cancellationToken);

        // Fraud screening — declines abort before Stripe; reviews proceed with flag
        var fraudResult = await _fraudService.EvaluateAsync(
            payment.Id, payment.Amount, payment.Currency, payment.CustomerEmail, cancellationToken);

        if (fraudResult.Decision == Fraud.FraudDecision.Decline)
        {
            payment.Fail();
            await _repository.UpdateAsync(payment, cancellationToken);
            _logger.LogWarning(
                "Payment {PaymentId} declined by fraud screening — score {Score} rules [{Rules}]",
                payment.Id, fraudResult.Score, string.Join(", ", fraudResult.RulesTriggered));
            throw new FraudDeclinedException(fraudResult.Score, fraudResult.RulesTriggered);
        }

        if (fraudResult.Decision == Fraud.FraudDecision.Review)
        {
            _logger.LogInformation(
                "Payment {PaymentId} flagged for fraud review — score {Score} rules [{Rules}] — proceeding",
                payment.Id, fraudResult.Score, string.Join(", ", fraudResult.RulesTriggered));
        }

        // Per-request mutable state captured by the retry callback closure
        var attemptNo = 1;
        var currentAttempt = PaymentAttempt.Start(payment.Id, attemptNo, "stripe");

        var retryPipeline = new ResiliencePipelineBuilder<ProviderResult>()
            .AddRetry(new RetryStrategyOptions<ProviderResult>
            {
                ShouldHandle = new PredicateBuilder<ProviderResult>()
                    .HandleResult(r => !r.Success && r.IsTransient),
                MaxRetryAttempts = MaxProviderRetries,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = async args =>
                {
                    var failed = args.Outcome.Result!;
                    currentAttempt.Fail(failed.ErrorCode, failed.ErrorMessage, failed.IsTransient);
                    await _repository.AddAttemptAsync(currentAttempt, cancellationToken);
                    attemptNo++;
                    currentAttempt = PaymentAttempt.Start(payment.Id, attemptNo, "stripe");

                    _logger.LogWarning(
                        "Transient provider failure on attempt {AttemptNo} for payment {PaymentId} trace {TraceId}. " +
                        "Retrying in {RetryDelay:0.0}s (error: {ErrorCode})",
                        args.AttemptNumber + 1,
                        payment.Id,
                        request.TraceId,
                        args.RetryDelay.TotalSeconds,
                        failed.ErrorCode);
                }
            })
            .Build();

        ProviderResult providerResult;
        try
        {
            // Circuit breaker (singleton) wraps retry (per-request) — breaker trips after exhausted retry batches
            providerResult = await _circuitBreaker.ExecuteAsync(async ct =>
                await retryPipeline.ExecuteAsync(
                    async ct2 => await _provider.AuthorizeAsync(request.Amount, request.Currency, request.PaymentMethodToken, ct2),
                    ct),
                cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning(
                "Circuit breaker open for payment {PaymentId} trace {TraceId} — failing fast",
                payment.Id, request.TraceId);
            payment.Fail();
            currentAttempt.Fail("circuit_open", "Payment service temporarily unavailable — circuit breaker open.", true);
            await _repository.UpdateAsync(payment, cancellationToken);
            await _repository.AddAttemptAsync(currentAttempt, cancellationToken);
            throw new PaymentProviderException("circuit_open", "Payment service temporarily unavailable.", true);
        }

        if (!providerResult.Success)
        {
            payment.Fail();
            currentAttempt.Fail(providerResult.ErrorCode, providerResult.ErrorMessage, providerResult.IsTransient);
            await _repository.UpdateAsync(payment, cancellationToken);
            await _repository.AddAttemptAsync(currentAttempt, cancellationToken);
            throw new PaymentProviderException(providerResult.ErrorCode, providerResult.ErrorMessage, providerResult.IsTransient);
        }

        payment.Authorize(providerResult.ProviderReference!);
        currentAttempt.Succeed();

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
            CreatedAtUtc: payment.CreatedAtUtc,
            FraudDecision: fraudResult.Decision,
            FraudScore: fraudResult.Score);

        if (request.IdempotencyKeyEntity is { } idempotencyKey)
        {
            idempotencyKey.Complete(
                paymentId: payment.Id,
                responseJson: JsonSerializer.Serialize(result, JsonOptions),
                statusCode: 201);
            await _repository.CompleteWithIdempotencyAsync(payment, currentAttempt, outboxMessage, idempotencyKey, cancellationToken);
        }
        else
        {
            await _repository.CompletePaymentAsync(payment, currentAttempt, outboxMessage, cancellationToken);
        }

        return result;
    }
}
