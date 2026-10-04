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
[Route("api/v1/admin/audit-logs")]
[Authorize(Policy = Policies.ViewAuditLog)]
[EnableRateLimiting(RateLimitPolicies.BackOffice)]
public sealed class AdminAuditController(IBackOfficeQueries queries) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("The audit log, newest first, by action, by who acted and from and to (whole days in UTC, both included). Admins only.")]
    [ProducesResponseType<PagedResponse<AuditItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<IActionResult> List([FromQuery] AuditQuery query, CancellationToken cancellationToken) =>
        Ok(await queries.ListAuditAsync(query, cancellationToken));
}
