using LedgerPay.Api.Authorization;
using LedgerPay.Api.Errors;
using LedgerPay.Api.Extensions;
using LedgerPay.Api.RateLimiting;
using LedgerPay.Application.BackOffice;
using LedgerPay.Application.Common;
using LedgerPay.Application.Transactions;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LedgerPay.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = Policies.BackOffice)]
[EnableRateLimiting(RateLimitPolicies.BackOffice)]
public sealed class AdminUsersController(IBackOfficeQueries queries) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Find customers by part of a name, an email, a mobile number or a wallet number, and by whether the wallet is frozen or the account locked. Operators and admins only.")]
    [ProducesResponseType<PagedResponse<UserSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<IActionResult> List([FromQuery] UserSearchQuery query, CancellationToken cancellationToken) =>
        Ok(await queries.ListUsersAsync(query, cancellationToken));

    [HttpGet("{walletNumber}")]
    [EndpointSummary("One customer with the freeze reason and the latest transactions. Looking is written to the audit log. Operators and admins only.")]
    [ProducesResponseType<UserDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<IActionResult> Get(string walletNumber, CancellationToken cancellationToken)
    {
        if (User.UserId() is not { } actorId)
        {
            return Problems.Result(HttpContext, ErrorCodes.Unauthenticated);
        }

        if (!WalletNumbers.IsValid(walletNumber))
        {
            var errors = new Dictionary<string, string[]> { ["walletNumber"] = [ValidationRules.WalletNumberMessage] };
            return Problems.Result(HttpContext, ErrorCodes.ValidationFailed, errors);
        }

        var result = await queries.GetUserAsync(actorId, walletNumber, HttpContext.ToRequestInfo(), cancellationToken);
        return this.FromResult(result, value => Ok(value));
    }
}
