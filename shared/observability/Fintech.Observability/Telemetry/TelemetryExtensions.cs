using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Fintech.Observability.Telemetry;

public static class TelemetryExtensions
{
    /// <summary>
    /// Wires OpenTelemetry tracing and metrics for a service. Exports to Application Insights
    /// when APPLICATIONINSIGHTS_CONNECTION_STRING is configured; otherwise traces stay in-process.
    /// W3C TraceContext propagation is the OTel default — covers HTTP and Azure SDK calls.
    /// </summary>
    public static IServiceCollection AddFintechTelemetry(
        this IServiceCollection services,
        string serviceName,
        IConfiguration configuration)
    {
        // Required for Azure SDK to emit ActivitySource events (Service Bus, etc.)
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        var aiConnectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        var otel = services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName: serviceName));

        otel.WithTracing(t =>
        {
            t.AddSource(FintechActivitySource.Name);
            t.AddSource("Azure.*");
            t.AddAspNetCoreInstrumentation();
            t.AddHttpClientInstrumentation();
            // Default options never capture raw SQL text — safe for production
            t.AddSqlClientInstrumentation();

            if (!string.IsNullOrWhiteSpace(aiConnectionString))
                t.AddAzureMonitorTraceExporter(o => o.ConnectionString = aiConnectionString);
        });

        otel.WithMetrics(m =>
        {
            m.AddMeter(FintechMeter.Name);
            m.AddAspNetCoreInstrumentation();
            m.AddHttpClientInstrumentation();
            m.AddRuntimeInstrumentation();

            if (!string.IsNullOrWhiteSpace(aiConnectionString))
                m.AddAzureMonitorMetricExporter(o => o.ConnectionString = aiConnectionString);
        });

        return services;
    }
}
