using LedgerPay.Api.Authorization;
using LedgerPay.Api.Errors;
using LedgerPay.Api.Extensions;
using LedgerPay.Api.RateLimiting;
using LedgerPay.Application.TopUps;
using LedgerPay.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LedgerPay.Api.Controllers;

[ApiController]
[Route("api/v1/admin/topups")]
[Authorize(Policy = Policies.TopUp)]
[EnableRateLimiting(RateLimitPolicies.Money)]
public sealed class AdminTopUpsController(ITopUpService topUps) : ControllerBase
{
    [HttpPost]
    [EndpointSummary("Credit a customer wallet after a bank deposit. The bank reference can be used once. Operators only. Needs an Idempotency-Key header.")]
    [ProducesResponseType<TopUpResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, Problems.ContentType)]
    public async Task<IActionResult> TopUp(
        TopUpRequest request,
        [FromHeader(Name = ControllerResultExtensions.IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (User.UserId() is not { } operatorId)
        {
            return Problems.Result(HttpContext, ErrorCodes.Unauthenticated);
        }

        if (this.RejectBadIdempotencyKey(idempotencyKey) is { } badKey)
        {
            return badKey;
        }

        var result = await topUps.TopUpAsync(operatorId, idempotencyKey, request, HttpContext.ToRequestInfo(), cancellationToken);
        return this.FromResult(result, value => StatusCode(StatusCodes.Status201Created, value));
    }
}
