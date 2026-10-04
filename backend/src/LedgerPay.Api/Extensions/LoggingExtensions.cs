using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace LedgerPay.Api.Extensions;

// Structured logs as one JSON object a line on the console, set in the Serilog section of the settings.
// What may be logged: the method, the route pattern (never the path), the status, the time and the trace id.
// Never: passwords, tokens, request bodies, query strings, emails or phone numbers.
public static class LoggingExtensions
{
    private const string RoutePatternKey = "LedgerPay.RoutePattern";

    public static WebApplicationBuilder AddApiLogging(this WebApplicationBuilder builder)
    {
        // The logger belongs to this host and the global one is left alone, so two hosts in one process, as in
        // the integration tests, each keep their own log.
        builder.Host.UseSerilog((context, services, logger) => logger
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.With<RequestPathRemover>(), preserveStaticLogger: true);
        return builder;
    }

    // Remembers which endpoint matched. The exception handler clears the endpoint, so the log line at the end of a
    // request that failed could not tell it otherwise. It goes right after UseRouting.
    public static WebApplication UseRoutePatternForLogs(this WebApplication app)
    {
        app.Use((context, next) =>
        {
            context.Items[RoutePatternKey] = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            return next(context);
        });
        return app;
    }

    public static WebApplication UseApiRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
            options.MessageTemplate = "HTTP {RequestMethod} {RoutePattern} responded {StatusCode} in {Elapsed:0.0000} ms";

            // The route pattern, such as api/v1/transactions/{reference}, says which endpoint ran without the
            // reference, wallet number or query string the caller put in the address.
            options.GetMessageTemplateProperties = (http, _, elapsed, statusCode) =>
            [
                new LogEventProperty("RequestMethod", new ScalarValue(http.Request.Method)),
                new LogEventProperty("RoutePattern", new ScalarValue(http.Items[RoutePatternKey] as string ?? "(no route)")),
                new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
                new LogEventProperty("Elapsed", new ScalarValue(elapsed))
            ];

            // A health check is polled all the time, so it is only logged when someone asks for more detail.
            options.GetLevel = (http, _, error) =>
                error is not null || http.Response.StatusCode >= StatusCodes.Status500InternalServerError ? LogEventLevel.Error
                : http.Request.Path.StartsWithSegments(HealthExtensions.Path) ? LogEventLevel.Verbose
                : LogEventLevel.Information;
        });
        return app;
    }

    // ASP.NET Core puts the request path on every log event of a request. The path can hold a wallet number or a
    // transaction reference, so it is taken off. The trace id is what ties a line to a request.
    private sealed class RequestPathRemover : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) => logEvent.RemovePropertyIfPresent("RequestPath");
    }
}
