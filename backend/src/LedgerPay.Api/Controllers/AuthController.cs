using LedgerPay.Api.Errors;
using LedgerPay.Api.Extensions;
using LedgerPay.Api.RateLimiting;
using LedgerPay.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LedgerPay.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(IAuthService auth) : ControllerBase
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
    [EndpointSummary("Sign in and get an access token that lasts 15 minutes. Anyone may call it.")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked, Problems.ContentType)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, HttpContext.ToRequestInfo(), cancellationToken);
        return this.FromResult(result, value => Ok(value));
    }
}
