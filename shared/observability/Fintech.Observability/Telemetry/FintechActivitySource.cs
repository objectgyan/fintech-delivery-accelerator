using System.Diagnostics;

namespace Fintech.Observability.Telemetry;

/// <summary>
/// Shared ActivitySource for application-level spans across all FinTech services.
/// Add custom spans via FintechActivitySource.Source.StartActivity("operation-name").
/// </summary>
public static class FintechActivitySource
{
    public const string Name = "Fintech.Application";
    public static readonly ActivitySource Source = new(Name);
}
