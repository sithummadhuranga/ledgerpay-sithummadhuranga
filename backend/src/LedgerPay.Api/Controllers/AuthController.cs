using LedgerPay.Api.Errors;
using LedgerPay.Api.Extensions;
using LedgerPay.Api.RateLimiting;
using LedgerPay.Api.Sessions;
using LedgerPay.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LedgerPay.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(IAuthService auth, ISessionService sessions, RefreshCookie cookie) : ControllerBase
{
    [HttpPost("register")]
    [EndpointSummary("Create a customer account with an empty wallet. Anyone may call it.")]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(request, HttpContext.ToRequestInfo(), cancellationToken);
        return this.FromResult(result, value => StatusCode(StatusCodes.Status201Created, value));
    }

    [HttpPost("login")]
    [EndpointSummary("Sign in and get an access token that lasts 15 minutes. The refresh token is set as an HttpOnly cookie. Anyone may call it.")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked, Problems.ContentType)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, HttpContext.ToRequestInfo(), cancellationToken);
        return this.FromResult(result, signedIn =>
        {
            cookie.Set(Response, signedIn.Refresh);
            return Ok(signedIn.Response);
        });
    }

    [HttpPost("refresh")]
    [SameOriginOnly]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    [EndpointSummary("Swap the refresh cookie for a new access token and a new cookie. Anyone with a cookie may call it.")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, Problems.ContentType)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var result = await sessions.RefreshAsync(cookie.Read(Request), HttpContext.ToRequestInfo(), cancellationToken);
        if (!result.Succeeded)
        {
            // A cookie that no longer works is removed, so the browser stops sending it.
            cookie.Clear(Response);
        }

        return this.FromResult(result, refreshed =>
        {
            if (refreshed.Refresh is not null)
            {
                cookie.Set(Response, refreshed.Refresh);
            }

            return Ok(refreshed.Response);
        });
    }

    [HttpPost("logout")]
    [SameOriginOnly]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    [EndpointSummary("End the session of the refresh cookie and remove the cookie. Always answers 204.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, Problems.ContentType)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sessions.EndAsync(cookie.Read(Request), HttpContext.ToRequestInfo(), cancellationToken);
        cookie.Clear(Response);
        return NoContent();
    }
}
