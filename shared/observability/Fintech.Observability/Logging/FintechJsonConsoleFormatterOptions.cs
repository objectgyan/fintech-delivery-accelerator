using Microsoft.Extensions.Logging.Console;

namespace Fintech.Observability.Logging;

public sealed class FintechJsonConsoleFormatterOptions : ConsoleFormatterOptions
{
    public string ServiceName { get; set; } = "unknown";
}
