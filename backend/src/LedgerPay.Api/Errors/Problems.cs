using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace LedgerPay.Api.Errors;

// The one error shape of the API: RFC 7807 Problem Details with a stable code and the trace id.
// The status and the title come from the error catalog, so every layer says the same thing.
public static class Problems
{
    public const string ContentType = "application/problem+json";

    public static ProblemDetails Create(
        HttpContext context,
        string code,
        IDictionary<string, string[]>? errors = null,
        int? retryAfterSeconds = null)
    {
        var info = ErrorCatalog.Describe(code);
        var problem = new ProblemDetails { Status = info.Status, Title = info.Title };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        if (retryAfterSeconds is not null)
        {
            problem.Extensions["retryAfterSeconds"] = retryAfterSeconds;
        }

        return problem;
    }

    public static ObjectResult Result(
        HttpContext context, string code, IDictionary<string, string[]>? errors = null, int? retryAfterSeconds = null)
    {
        if (retryAfterSeconds is not null)
        {
            context.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var problem = Create(context, code, errors, retryAfterSeconds);
        return new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { ContentType }
        };
    }

    // The code for an error the framework raised itself, with an empty body. Other statuses are left alone.
    public static string? CodeForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.ValidationFailed,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status405MethodNotAllowed => ErrorCodes.MethodNotAllowed,
        StatusCodes.Status413PayloadTooLarge => ErrorCodes.PayloadTooLarge,
        StatusCodes.Status415UnsupportedMediaType => ErrorCodes.UnsupportedMediaType,
        _ => null
    };

    // For code that runs outside MVC, such as the exception handler and the token events.
    public static Task WriteAsync(HttpContext context, string code, CancellationToken cancellationToken = default)
    {
        var problem = Create(context, code);
        context.Response.StatusCode = problem.Status!.Value;
        return context.Response.WriteAsJsonAsync(problem, options: null, contentType: ContentType, cancellationToken);
    }
}
