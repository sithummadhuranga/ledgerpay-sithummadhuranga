using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace LedgerPay.IntegrationTests.Support;

// Keeps every log event the app writes: the rendered message, all its properties and the text of any exception,
// so a test can look for data that must not be there.
public sealed class LogCollector : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> events = new();

    public IReadOnlyCollection<LogEvent> Events => events.ToArray();

    public IReadOnlyCollection<string> Lines => events.Select(Describe).ToArray();

    public void Emit(LogEvent logEvent) => events.Enqueue(logEvent);

    private static string Describe(LogEvent logEvent)
    {
        var properties = string.Join(" ", logEvent.Properties.Select(property => $"{property.Key}={property.Value}"));
        return $"[{logEvent.Level}] {logEvent.RenderMessage()} {properties} {logEvent.Exception}";
    }
}
