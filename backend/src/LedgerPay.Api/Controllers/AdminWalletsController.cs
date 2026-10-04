using LedgerPay.Api.Authorization;
using LedgerPay.Api.Errors;
using LedgerPay.Api.Extensions;
using LedgerPay.Application.Admin;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerPay.Api.Controllers;

[ApiController]
[Route("api/v1/admin/wallets")]
[Authorize(Policy = Policies.FreezeWallet)]
public sealed class AdminWalletsController(IWalletStatusService walletStatus) : ControllerBase
{
    [HttpPatch("{walletNumber}/status")]
    [EndpointSummary("Freeze or unfreeze a wallet, with a reason. Operators and admins only.")]
    [ProducesResponseType<WalletStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    public async Task<IActionResult> SetStatus(string walletNumber, WalletStatusRequest request, CancellationToken cancellationToken)
    {
        if (User.UserId() is not { } actorId)
        {
            return Problems.Result(HttpContext, ErrorCodes.Unauthenticated);
        }

        // The service looks the wallet up as it is given, so the format is checked here.
        if (!WalletNumbers.IsValid(walletNumber))
        {
            var errors = new Dictionary<string, string[]> { ["walletNumber"] = [ValidationRules.WalletNumberMessage] };
            return Problems.Result(HttpContext, ErrorCodes.ValidationFailed, errors);
        }

        var result = await walletStatus.SetStatusAsync(actorId, walletNumber, request, HttpContext.ToRequestInfo(), cancellationToken);
        return this.FromResult(result, value => Ok(value));
    }
}
