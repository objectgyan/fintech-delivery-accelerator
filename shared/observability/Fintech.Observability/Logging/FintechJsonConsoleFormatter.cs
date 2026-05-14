using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Fintech.Observability.Logging;

/// <summary>
/// Emits one JSON object per log entry matching the FinTech log schema:
/// timestampUtc, level, service, traceId, operation, message, plus structured props.
/// </summary>
public sealed class FintechJsonConsoleFormatter : ConsoleFormatter, IDisposable
{
    public const string FormatterName = "fintech-json";

    private readonly IDisposable? _optionsReloadToken;
    private FintechJsonConsoleFormatterOptions _options;

    public FintechJsonConsoleFormatter(IOptionsMonitor<FintechJsonConsoleFormatterOptions> options)
        : base(FormatterName)
    {
        _options = options.CurrentValue;
        _optionsReloadToken = options.OnChange(o => _options = o);
    }

    public void Dispose() => _optionsReloadToken?.Dispose();

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (string.IsNullOrEmpty(message) && logEntry.Exception is null)
            return;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("timestampUtc", DateTimeOffset.UtcNow.ToString("O"));
            writer.WriteString("level", logEntry.LogLevel.ToString());
            writer.WriteString("service", _options.ServiceName);

            var activity = Activity.Current;
            if (activity is not null)
            {
                writer.WriteString("traceId", activity.TraceId.ToString());
                if (activity.SpanId != default)
                    writer.WriteString("spanId", activity.SpanId.ToString());
            }

            // Find operation in state or scopes (state wins)
            var operation = FindOperation(logEntry.State, scopeProvider);
            writer.WriteString("operation", operation ?? ShortCategory(logEntry.Category));

            writer.WriteString("category", logEntry.Category);
            writer.WriteString("message", message ?? string.Empty);

            // Structured properties (exclude noisy / sensitive keys)
            writer.WritePropertyName("props");
            writer.WriteStartObject();
            WriteStateProps(writer, logEntry.State);
            WriteScopeProps(writer, scopeProvider);
            writer.WriteEndObject();

            if (logEntry.Exception is not null)
            {
                writer.WriteString("exceptionType", logEntry.Exception.GetType().FullName);
                writer.WriteString("exceptionMessage", logEntry.Exception.Message);
                // Stack trace deliberately omitted — CLAUDE.md: never log raw stack traces
            }

            writer.WriteEndObject();
        }

        stream.Position = 0;
        textWriter.WriteLine(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static string? FindOperation<TState>(TState state, IExternalScopeProvider? scopeProvider)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> stateProps)
        {
            foreach (var kvp in stateProps)
                if (kvp.Key.Equals("operation", StringComparison.OrdinalIgnoreCase))
                    return kvp.Value?.ToString();
        }

        string? scopeOperation = null;
        scopeProvider?.ForEachScope((scope, _) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> scopeProps)
            {
                foreach (var kvp in scopeProps)
                    if (kvp.Key.Equals("operation", StringComparison.OrdinalIgnoreCase))
                        scopeOperation = kvp.Value?.ToString();
            }
        }, 0);
        return scopeOperation;
    }

    private static void WriteStateProps<TState>(Utf8JsonWriter writer, TState state)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> props) return;
        foreach (var kvp in props)
        {
            if (kvp.Key == "{OriginalFormat}") continue;
            if (kvp.Key.Equals("operation", StringComparison.OrdinalIgnoreCase)) continue;
            WriteValue(writer, kvp.Key, kvp.Value);
        }
    }

    private static void WriteScopeProps(Utf8JsonWriter writer, IExternalScopeProvider? scopeProvider)
    {
        if (scopeProvider is null) return;
        scopeProvider.ForEachScope((scope, w) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> props)
            {
                foreach (var kvp in props)
                {
                    if (kvp.Key == "{OriginalFormat}") continue;
                    if (kvp.Key.Equals("operation", StringComparison.OrdinalIgnoreCase)) continue;
                    WriteValue(w, kvp.Key, kvp.Value);
                }
            }
        }, writer);
    }

    private static void WriteValue(Utf8JsonWriter writer, string key, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNull(key); break;
            case string s: writer.WriteString(key, s); break;
            case int i: writer.WriteNumber(key, i); break;
            case long l: writer.WriteNumber(key, l); break;
            case double d: writer.WriteNumber(key, d); break;
            case decimal dec: writer.WriteNumber(key, dec); break;
            case bool b: writer.WriteBoolean(key, b); break;
            case Guid g: writer.WriteString(key, g.ToString()); break;
            case DateTimeOffset dto: writer.WriteString(key, dto.ToString("O")); break;
            case DateTime dt: writer.WriteString(key, dt.ToString("O")); break;
            default: writer.WriteString(key, value.ToString() ?? string.Empty); break;
        }
    }

    private static string ShortCategory(string category)
    {
        var lastDot = category.LastIndexOf('.');
        return lastDot < 0 ? category : category[(lastDot + 1)..];
    }
}
