using LedgerPay.Api.Authorization;
using LedgerPay.Api.Errors;
using LedgerPay.Api.Extensions;
using LedgerPay.Application.Transactions;
using LedgerPay.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerPay.Api.Controllers;

[ApiController]
[Route("api/v1/transactions")]
[Authorize]
public sealed class TransactionsController(ITransactionQueries transactions, IAuthorizationService authorization) : ControllerBase
{
    [HttpGet("{reference}")]
    [EndpointSummary("Show one transaction. Its sender and receiver see it, and so do operators and admins. Anyone else gets 404, the same as for a reference that does not exist.")]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<IActionResult> Get(string? reference, CancellationToken cancellationToken)
    {
        if (User.UserId() is not { } userId)
        {
            return Problems.Result(HttpContext, ErrorCodes.Unauthenticated);
        }

        var isBackOffice = (await authorization.AuthorizeAsync(User, Policies.ViewAnyTransaction)).Succeeded;
        var result = await transactions.GetByReferenceAsync(userId, isBackOffice, reference ?? string.Empty, cancellationToken);
        return this.FromResult(result, value => Ok(value));
    }
}
