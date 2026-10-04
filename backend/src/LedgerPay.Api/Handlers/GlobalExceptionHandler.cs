using LedgerPay.Api.Errors;
using LedgerPay.Domain.Constants;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Api.Handlers;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // A client that went away is not a server fault.
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        // The framework refused the request itself, for example because the body was too large. It is the client's
        // mistake, so it is not logged as an error, and the client gets the status the framework chose.
        if (exception is BadHttpRequestException badRequest)
        {
            logger.LogInformation("Bad request, status {Status}, trace id {TraceId}", badRequest.StatusCode, context.TraceIdentifier);
            await Problems.WriteAsync(context, Problems.CodeForStatus(badRequest.StatusCode) ?? ErrorCodes.ValidationFailed, cancellationToken);
            return true;
        }

        // The text of a database error can hold the value that broke a constraint, such as an email, so only
        // the type and the stack are logged for it. Anything else is logged whole.
        if (IsDatabaseError(exception))
        {
            logger.LogError(
                "Database error {ExceptionType}, trace id {TraceId}{NewLine}{StackTrace}",
                exception.GetType().Name, context.TraceIdentifier, Environment.NewLine, exception.StackTrace);
        }
        else
        {
            logger.LogError(exception, "Unhandled exception, trace id {TraceId}", context.TraceIdentifier);
        }

        await Problems.WriteAsync(context, ErrorCodes.InternalError, cancellationToken);
        return true;
    }

    private static bool IsDatabaseError(Exception? exception) =>
        exception is not null && (exception is DbUpdateException or System.Data.Common.DbException || IsDatabaseError(exception.InnerException));
}
