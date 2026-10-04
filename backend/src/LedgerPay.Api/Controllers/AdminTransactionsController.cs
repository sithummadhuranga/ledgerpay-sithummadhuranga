using LedgerPay.Api.Authorization;
using LedgerPay.Api.Errors;
using LedgerPay.Api.RateLimiting;
using LedgerPay.Application.BackOffice;
using LedgerPay.Application.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LedgerPay.Api.Controllers;

[ApiController]
[Route("api/v1/admin/transactions")]
[Authorize(Policy = Policies.BackOffice)]
[EnableRateLimiting(RateLimitPolicies.BackOffice)]
public sealed class AdminTransactionsController(IBackOfficeQueries queries) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Every transaction, newest first, with type, status, wallet number and from and to (whole days in UTC, both included). Operators and admins only.")]
    [ProducesResponseType<PagedResponse<StaffTransactionItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<IActionResult> List([FromQuery] StaffTransactionQuery query, CancellationToken cancellationToken) =>
        Ok(await queries.ListTransactionsAsync(query, cancellationToken));
}
