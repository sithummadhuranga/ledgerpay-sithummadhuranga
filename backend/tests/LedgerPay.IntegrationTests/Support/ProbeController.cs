using LedgerPay.Domain.Constants;
using LedgerPay.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Support;

// Endpoints that exist only in the tests, to try the token checks, the role check and the error handler
// before the real endpoints that use them are built.
[ApiController]
[Route("api/v1/probe")]
public sealed class ProbeController : ControllerBase
{
    [HttpGet("signed-in")]
    [Authorize]
    public IActionResult SignedIn() => Ok(new
    {
        userId = User.FindFirst(JwtClaimNames.Subject)?.Value,
        roles = User.FindAll(JwtClaimNames.Role).Select(claim => claim.Value).ToArray()
    });

    [HttpGet("operators")]
    [Authorize(Roles = RoleNames.Operator)]
    public IActionResult Operators() => Ok(new { ok = true });

    // No attribute on purpose: the default policy must still ask for a token.
    [HttpGet("unmarked")]
    public IActionResult Unmarked() => Ok(new { ok = true });

    [HttpGet("anyone")]
    [AllowAnonymous]
    public IActionResult Anyone() => Ok(new { ok = true });

    [HttpGet("db-boom")]
    [AllowAnonymous]
    public IActionResult DatabaseBoom() => throw new DbUpdateException(
        "Violation of UNIQUE KEY constraint. The duplicate key value is (leaky.person@example.com).",
        new InvalidOperationException("duplicate key value is (leaky.person@example.com)"));

    [HttpGet("bad-request")]
    [AllowAnonymous]
    public IActionResult BadRequestBoom() => throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);

    [HttpGet("boom")]
    [AllowAnonymous]
    public IActionResult Boom() => throw new InvalidOperationException("secret detail that must never reach a client");
}
