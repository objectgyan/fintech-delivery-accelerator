using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Fintech.Observability.Logging;

public static class LoggingExtensions
{
    /// <summary>
    /// Replaces the default console logger with the FinTech JSON formatter.
    /// Sets the service name on every log entry. Reads "Logging:FintechJson" config section for overrides.
    /// </summary>
    public static ILoggingBuilder AddFintechJsonLogging(this ILoggingBuilder builder, string serviceName, IConfiguration? configuration = null)
    {
        builder.ClearProviders();
        builder.AddConsole(options => options.FormatterName = FintechJsonConsoleFormatter.FormatterName);
        builder.AddConsoleFormatter<FintechJsonConsoleFormatter, FintechJsonConsoleFormatterOptions>(options =>
        {
            options.ServiceName = serviceName;
            options.IncludeScopes = true;
            configuration?.GetSection("Logging:FintechJson").Bind(options);
        });
        return builder;
    }
}
