using Microsoft.Extensions.Logging;
using Payment.Application.Providers;

namespace Payment.Infrastructure.Providers;

/// <summary>
/// Dev-only decorator that injects configurable transient failures so retry and
/// circuit-breaker behaviour can be observed without a real Stripe outage.
/// Never registered outside Development environment.
/// </summary>
internal sealed class SimulatedPaymentProvider : IPaymentProvider
{
    private readonly IPaymentProvider _inner;
    private readonly ILogger<SimulatedPaymentProvider> _logger;
    private readonly int _failCount;

    private int _callCount;

    public SimulatedPaymentProvider(
        IPaymentProvider inner,
        ILogger<SimulatedPaymentProvider> logger,
        int failCount)
    {
        _inner = inner;
        _logger = logger;
        _failCount = failCount;
    }

    public async Task<ProviderResult> AuthorizeAsync(
        decimal amount,
        string currency,
        string paymentMethodToken,
        CancellationToken cancellationToken = default)
    {
        var callNo = Interlocked.Increment(ref _callCount);

        if (callNo <= _failCount)
        {
            _logger.LogWarning(
                "[SIMULATION] Injecting transient failure on call {CallNo}/{FailCount}",
                callNo, _failCount);

            return new ProviderResult(
                Success: false,
                ProviderReference: null,
                ErrorCode: "simulated_transient_error",
                ErrorMessage: $"Simulated transient failure (call {callNo} of {_failCount})",
                IsTransient: true);
        }

        _logger.LogInformation(
            "[SIMULATION] Call {CallNo} passes through to real provider (simulation exhausted after {FailCount} failures)",
            callNo, _failCount);

        return await _inner.AuthorizeAsync(amount, currency, paymentMethodToken, cancellationToken);
    }
}
