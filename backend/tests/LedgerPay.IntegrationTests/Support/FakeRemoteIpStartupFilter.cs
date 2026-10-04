using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace LedgerPay.IntegrationTests.Support;

// The in-memory test server has no client address. This gives every request one, so the audit log has an IP to check.
public sealed class FakeRemoteIpStartupFilter : IStartupFilter
{
    public const string Address = "203.0.113.77";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(Address);
            return nextMiddleware(context);
        });

        next(app);
    };
}
