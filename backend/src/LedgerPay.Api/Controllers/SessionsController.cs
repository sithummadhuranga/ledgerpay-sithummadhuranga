using LedgerPay.Api.Errors;
using LedgerPay.Api.Extensions;
using LedgerPay.Api.RateLimiting;
using LedgerPay.Api.Sessions;
using LedgerPay.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LedgerPay.Api.Controllers;

// The places a user is signed in. The cookie reaches these routes because they are under the auth path, which is how
// the list can say which one is this browser.
[ApiController]
[Route("api/v1/auth/sessions")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Lookup)]
public sealed class SessionsController(ISessionService sessions, RefreshCookie cookie) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("My signed-in sessions, newest first, with the one of this browser marked. Any signed-in user.")]
    [ProducesResponseType<IReadOnlyList<SessionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var list = await sessions.ListAsync(User.UserId()!.Value, cookie.Read(Request), cancellationToken);
        return Ok(list);
    }

    [HttpDelete("{id:guid}")]
    [SameOriginOnly]
    [EndpointSummary("Sign out one of my sessions. Any signed-in user, for their own sessions only.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var result = await sessions.RevokeAsync(User.UserId()!.Value, id, cookie.Read(Request), HttpContext.ToRequestInfo(), cancellationToken);
        return this.FromResult(result, wasCurrent =>
        {
            if (wasCurrent)
            {
                cookie.Clear(Response);
            }

            return NoContent();
        });
    }
}
