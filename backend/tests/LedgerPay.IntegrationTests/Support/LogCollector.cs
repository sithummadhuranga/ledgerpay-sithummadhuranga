using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace LedgerPay.IntegrationTests.Support;

// Keeps every log line the app writes, with the text of any exception, so a test can look for data that must not be there.
public sealed class LogCollector : ILoggerProvider
{
    private readonly ConcurrentQueue<string> lines = new();

    public IReadOnlyCollection<string> Lines => lines.ToArray();

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, lines);

    public void Dispose()
    {
    }

    private sealed class CollectingLogger(string category, ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lines.Enqueue($"[{logLevel}] {category}: {formatter(state, exception)} {exception}");
        }
    }
}
