using LedgerPay.Api.Errors;
using LedgerPay.Domain.Constants;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LedgerPay.Api.Sessions;

// The refresh cookie is SameSite=Strict, which already keeps other sites from using it. This is the second lock:
// a browser says where a request started in Sec-Fetch-Site, and a request that did not start on this origin is refused.
// A client that sends no such header (curl, a test) is allowed, because it has no cookie jar for another site to borrow.
[AttributeUsage(AttributeTargets.Method)]
public sealed class SameOriginOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var site = context.HttpContext.Request.Headers["Sec-Fetch-Site"].ToString();
        if (site is "" or "same-origin" or "none")
        {
            return;
        }

        context.Result = Problems.Result(context.HttpContext, ErrorCodes.Forbidden);
    }
}
