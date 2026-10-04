using LedgerPay.Api.Errors;
using LedgerPay.Application.Common;
using LedgerPay.Application.Idempotency;
using LedgerPay.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace LedgerPay.Api.Extensions;

public static class ControllerResultExtensions
{
    public const string ReplayedHeader = "Idempotent-Replayed";
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    // Turns what a service answered into an HTTP answer: the success the controller builds, or the Problem Details
    // for the error code. An answer that was stored under an Idempotency-Key is marked with a header.
    public static IActionResult FromResult<T>(this ControllerBase controller, ServiceResult<T> result, Func<T, IActionResult> onSuccess)
    {
        if (result.Replayed)
        {
            controller.Response.Headers[ReplayedHeader] = "true";
        }

        return result.Succeeded
            ? onSuccess(result.Value!)
            : Problems.Result(controller.HttpContext, result.ErrorCode!, retryAfterSeconds: result.RetryAfterSeconds);
    }

    // Checks the Idempotency-Key header before the service runs, so a key that cannot be used is reported with the
    // header named. The services check it again, because they must not trust the caller.
    public static IActionResult? RejectBadIdempotencyKey(this ControllerBase controller, string? key)
    {
        var code = IdempotencyService.ValidateKey(key);
        if (code is null)
        {
            return null;
        }

        var errors = code == ErrorCodes.ValidationFailed
            ? new Dictionary<string, string[]>
            {
                [IdempotencyKeyHeader] = [$"Use at most {IdempotencyService.MaximumKeyLength} characters from A-Z, a-z, 0-9, _ and -."]
            }
            : null;
        return Problems.Result(controller.HttpContext, code, errors);
    }

    public static RequestInfo ToRequestInfo(this HttpContext context) =>
        new(context.Connection.RemoteIpAddress?.ToString(), context.TraceIdentifier);
}
