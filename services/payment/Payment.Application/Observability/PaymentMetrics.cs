using System.Diagnostics.Metrics;
using Fintech.Observability.Telemetry;

namespace Payment.Application.Observability;

/// <summary>
/// Custom metrics for the payment service. Exported via OTel to App Insights.
/// </summary>
public sealed class PaymentMetrics
{
    public Counter<long> Requests { get; }
    public Counter<long> Successes { get; }
    public Counter<long> Failures { get; }
    public Counter<long> StripeErrors { get; }
    public Counter<long> Retries { get; }
    public Histogram<double> LatencyMs { get; }

    public PaymentMetrics()
    {
        var meter = FintechMeter.Instance;
        Requests = meter.CreateCounter<long>("payment.request.count", description: "POST /payments received");
        Successes = meter.CreateCounter<long>("payment.success.count", description: "Payments authorised");
        Failures = meter.CreateCounter<long>("payment.failure.count", description: "Payments that failed (Stripe / fraud / circuit-open)");
        StripeErrors = meter.CreateCounter<long>("payment.stripe_error.count", description: "Stripe provider errors (any classification)");
        Retries = meter.CreateCounter<long>("payment.retry.count", description: "Polly retries triggered (transient Stripe failures)");
        LatencyMs = meter.CreateHistogram<double>("payment.latency.ms", unit: "ms", description: "End-to-end POST /payments latency");
    }
}
