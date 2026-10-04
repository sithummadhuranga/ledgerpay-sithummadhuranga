using LedgerPay.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace LedgerPay.Api.Authorization;

// Every permission has a name here, and this is the one place that says which roles hold it.
// An endpoint asks for the permission, never for a role.
public static class Policies
{
    public const string CustomerActions = "CustomerActions";
    public const string TopUp = "TopUp";
    public const string FreezeWallet = "FreezeWallet";
    public const string ViewAnyTransaction = "ViewAnyTransaction";

    public static void Register(AuthorizationOptions options)
    {
        options.AddPolicy(CustomerActions, policy => policy.RequireRole(RoleNames.Customer));
        options.AddPolicy(TopUp, policy => policy.RequireRole(RoleNames.Operator));
        options.AddPolicy(FreezeWallet, policy => policy.RequireRole(RoleNames.Operator, RoleNames.Admin));
        options.AddPolicy(ViewAnyTransaction, policy => policy.RequireRole(RoleNames.Operator, RoleNames.Admin));
    }
}
