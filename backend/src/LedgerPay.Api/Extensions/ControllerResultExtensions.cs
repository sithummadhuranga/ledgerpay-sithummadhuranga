using LedgerPay.Api.Errors;
using LedgerPay.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace LedgerPay.Api.Extensions;

public static class ControllerResultExtensions
{
    public const string ReplayedHeader = "Idempotent-Replayed";

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

    public static RequestInfo ToRequestInfo(this HttpContext context) =>
        new(context.Connection.RemoteIpAddress?.ToString(), context.TraceIdentifier);
}
