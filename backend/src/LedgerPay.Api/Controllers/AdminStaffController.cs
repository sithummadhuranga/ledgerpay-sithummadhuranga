using LedgerPay.Api.Authorization;
using LedgerPay.Api.Errors;
using LedgerPay.Api.Extensions;
using LedgerPay.Api.RateLimiting;
using LedgerPay.Application.Admin;
using LedgerPay.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LedgerPay.Api.Controllers;

[ApiController]
[Route("api/v1/admin/staff")]
[Authorize(Policy = Policies.ManageStaff)]
[EnableRateLimiting(RateLimitPolicies.BackOffice)]
public sealed class AdminStaffController(IStaffService staff) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("The operator and admin accounts, with whether each is restricted. Admins only.")]
    [ProducesResponseType<IReadOnlyList<StaffMember>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await staff.ListAsync(cancellationToken));

    [HttpPatch("restriction")]
    [EndpointSummary("Restrict an operator account, or lift the restriction, with a reason. A restricted operator cannot sign in, their sessions end at once and their token stops working. Admins only.")]
    [ProducesResponseType<StaffMember>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, Problems.ContentType)]
    public async Task<IActionResult> SetRestriction(StaffRestrictionRequest request, CancellationToken cancellationToken)
    {
        if (User.UserId() is not { } actorId)
        {
            return Problems.Result(HttpContext, ErrorCodes.Unauthenticated);
        }

        var result = await staff.SetRestrictionAsync(actorId, request, HttpContext.ToRequestInfo(), cancellationToken);
        return this.FromResult(result, value => Ok(value));
    }
}
