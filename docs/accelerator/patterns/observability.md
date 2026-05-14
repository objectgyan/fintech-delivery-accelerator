# Pattern: Observability Stack

A working observability story for a small distributed system: structured logs + distributed traces + custom metrics + alerts, all flowing into Application Insights.

## What you get with this stack

- **One JSON line per log entry** — parseable by Log Analytics with no transformation
- **One trace ID per request** — copyable from the response, traceable across all services
- **8 custom metrics** — request count, success/failure, latency, retries, Stripe errors, outbox publish counts
- **2 alerts wired to email** — payment 5xx rate, Service Bus DLQ depth

## Files that do the work

| Concern | Where | Notes |
|---------|-------|-------|
| Shared lib | `shared/observability/Fintech.Observability/` | Logging + Telemetry namespaces |
| JSON formatter | `Logging/FintechJsonConsoleFormatter.cs` | Custom `ConsoleFormatter` — required schema + structured props |
| Single registration | `Logging/LoggingExtensions.AddFintechJsonLogging(name, config)` | One line per service |
| OTel registration | `Telemetry/TelemetryExtensions.AddFintechTelemetry(name, config)` | Tracing + metrics, Azure Monitor exporter when connection string set |
| Custom spans | `Telemetry/FintechActivitySource.Source` | `using var span = FintechActivitySource.Source.StartActivity("...")` |
| Custom metrics | `Telemetry/FintechMeter.Instance` | Each service creates its own counters / histograms |
| Service metrics | `Payment.Application/Observability/PaymentMetrics.cs` | DI singleton, injected into handler |
| Alerts | `infra/modules/alerts.bicep` | Two metric alerts + action group |
| Runbook | `docs/runbook.md` | What to do when an alert fires |

## Required log schema (every line)

```json
{
  "timestampUtc": "...",
  "level": "Information|Warning|Error",
  "service": "payment|fraud|notification",
  "traceId": "...",
  "spanId": "...",
  "operation": "CreatePayment",
  "category": "Payment.Application.Commands.CreatePayment.CreatePaymentCommandHandler",
  "message": "...",
  "props": { ... }
}
```

Stack traces are deliberately omitted — `exceptionType` + `exceptionMessage` go in, the full trace does not.

## TraceContext propagation

- **HTTP**: W3C `traceparent` header — OTel HttpClient instrumentation handles both inbound (AspNetCore) and outbound (HttpClient) automatically
- **Service Bus**: Azure SDK propagates `traceparent` in message ApplicationProperties — `AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true)` activates the diagnostic source

Receiver-side worker code does *not* need to manually link spans — the Azure SDK creates a child span when it picks up the message.

## Adding a metric

```csharp
// In <Service>.Application/Observability/<Service>Metrics.cs
public Counter<long> MyCounter { get; }
public <Service>Metrics() {
    MyCounter = FintechMeter.Instance.CreateCounter<long>("service.my.count");
}
```

Register as singleton in `DependencyInjection.cs`, inject, increment. Metric flows to App Insights via OTel exporter automatically — no manual config.

## Adding an alert

Add a new `Microsoft.Insights/metricAlerts` resource to `infra/modules/alerts.bicep`, pointing at the same action group. Bump the runbook to cover triage.
