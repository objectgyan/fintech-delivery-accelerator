using System.Diagnostics.Metrics;

namespace Fintech.Observability.Telemetry;

/// <summary>
/// Shared Meter for FinTech custom metrics (counters, histograms). Service-specific
/// metric names are conventionally prefixed: payment.*, fraud.*, notification.*.
/// </summary>
public static class FintechMeter
{
    public const string Name = "Fintech.Application";
    public static readonly Meter Instance = new(Name);
}
