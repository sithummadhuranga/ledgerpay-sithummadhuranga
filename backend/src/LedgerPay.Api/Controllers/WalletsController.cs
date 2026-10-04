using LedgerPay.Api.Authorization;
using LedgerPay.Api.Errors;
using LedgerPay.Api.Extensions;
using LedgerPay.Application.Transactions;
using LedgerPay.Application.Wallets;
using LedgerPay.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerPay.Api.Controllers;

[ApiController]
[Route("api/v1/wallets")]
[Authorize(Policy = Policies.CustomerActions)]
public sealed class WalletsController(IWalletService wallets, ITransactionQueries transactions) : ControllerBase
{
    [HttpGet("me")]
    [EndpointSummary("Show my wallet number, balance and status. Customers only.")]
    [ProducesResponseType<WalletResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (User.UserId() is not { } userId)
        {
            return Problems.Result(HttpContext, ErrorCodes.Unauthenticated);
        }

        var result = await wallets.GetMineAsync(userId, cancellationToken);
        return this.FromResult(result, value => Ok(value));
    }

    [HttpGet("lookup")]
    [EndpointSummary("Find a wallet by wallet number or mobile number before sending. Shows the number, a masked name and whether it is active. Customers only.")]
    [ProducesResponseType<LookupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<IActionResult> Lookup([FromQuery] LookupQuery query, CancellationToken cancellationToken)
    {
        var result = await wallets.LookupAsync(query, cancellationToken);
        return this.FromResult(result, value => Ok(value));
    }

    [HttpGet("me/transactions")]
    [EndpointSummary("My transaction history, newest first, with page, pageSize, from and to (whole days in UTC, both included). Customers only.")]
    [ProducesResponseType<PagedResponse<HistoryItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<IActionResult> History([FromQuery] HistoryQuery query, CancellationToken cancellationToken)
    {
        if (User.UserId() is not { } userId)
        {
            return Problems.Result(HttpContext, ErrorCodes.Unauthenticated);
        }

        var result = await transactions.HistoryAsync(userId, query, cancellationToken);
        return this.FromResult(result, value => Ok(value));
    }
}
