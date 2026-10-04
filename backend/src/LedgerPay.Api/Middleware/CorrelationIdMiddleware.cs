using System.Text.RegularExpressions;
using Serilog.Context;

namespace LedgerPay.Api.Middleware;

// Gives every request one id. It is the trace id in error answers, the correlation id in the audit log, the
// X-Correlation-Id response header and the TraceId on every log event, so a report from a user can be matched
// to a log line and an audit entry.
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    [GeneratedRegex(@"\A[A-Za-z0-9_-]{1,64}\z")]
    private static partial Regex SafeId();

    public Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].ToString();
        var id = SafeId().IsMatch(supplied) ? supplied : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = id;

        // Set when the response starts, because the exception handler clears the headers it finds.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        return RunAsync(context, id);
    }

    private async Task RunAsync(HttpContext context, string id)
    {
        using (LogContext.PushProperty("TraceId", id))
        {
            await next(context);
        }
    }
}
