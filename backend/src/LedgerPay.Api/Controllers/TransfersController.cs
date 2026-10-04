using LedgerPay.Api.Authorization;
using LedgerPay.Api.Errors;
using LedgerPay.Api.Extensions;
using LedgerPay.Application.Transfers;
using LedgerPay.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerPay.Api.Controllers;

[ApiController]
[Route("api/v1/transfers")]
[Authorize(Policy = Policies.CustomerActions)]
public sealed class TransfersController(ITransferService transfers) : ControllerBase
{
    [HttpGet("quote")]
    [EndpointSummary("Show the fee and the total for an amount, before sending. Customers only.")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, Problems.ContentType)]
    public async Task<IActionResult> Quote([FromQuery] QuoteRequest request, CancellationToken cancellationToken)
    {
        var result = await transfers.QuoteAsync(request.Amount, cancellationToken);
        return this.FromResult(result, value => Ok(value));
    }

    [HttpPost]
    [EndpointSummary("Send money to another active wallet, by wallet number or mobile number. The fee is charged to the sender. Needs an Idempotency-Key header.")]
    [ProducesResponseType<TransferResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, Problems.ContentType)]
    public async Task<IActionResult> Transfer(
        TransferRequest request,
        [FromHeader(Name = ControllerResultExtensions.IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (User.UserId() is not { } userId)
        {
            return Problems.Result(HttpContext, ErrorCodes.Unauthenticated);
        }

        if (this.RejectBadIdempotencyKey(idempotencyKey) is { } badKey)
        {
            return badKey;
        }

        var result = await transfers.TransferAsync(userId, idempotencyKey, request, HttpContext.ToRequestInfo(), cancellationToken);
        return this.FromResult(result, value => StatusCode(StatusCodes.Status201Created, value));
    }
}
