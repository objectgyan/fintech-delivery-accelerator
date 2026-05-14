using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Payment.Application.Observability;
using Payment.Application.Providers;
using Polly;
using Polly.CircuitBreaker;

namespace Payment.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddSingleton<PaymentMetrics>();

        // Singleton circuit breaker — shared state across all payment requests
        // Threshold: ≥50% transient failures across 5+ calls in a 30s window → break for 30s
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>()
                           .CreateLogger("Payment.StripeCircuitBreaker");

            return new ResiliencePipelineBuilder<ProviderResult>()
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions<ProviderResult>
                {
                    ShouldHandle = new PredicateBuilder<ProviderResult>()
                        .HandleResult(r => !r.Success && r.IsTransient),
                    MinimumThroughput = 5,
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(30),
                    OnOpened = args =>
                    {
                        logger.LogWarning(
                            "Stripe circuit breaker OPENED — break duration {BreakDuration:0.0}s",
                            args.BreakDuration.TotalSeconds);
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = args =>
                    {
                        logger.LogInformation("Stripe circuit breaker HALF-OPEN — probing next call");
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        logger.LogInformation("Stripe circuit breaker CLOSED");
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();
        });

        return services;
    }
}
