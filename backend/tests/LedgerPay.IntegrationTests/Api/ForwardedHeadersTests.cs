using System.Net.Http.Json;
using LedgerPay.Domain.Constants;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Api;

public class ForwardedHeadersTests(SqlServerFixture sql)
{
    private ApiFactory Factory(bool enabled) =>
        new(sql.ApiConnectionString, TestJwt.Options(), new Dictionary<string, string?> { ["ForwardedHeaders:Enabled"] = enabled ? "true" : "false" });

    // Sends one failed sign-in and returns the address that the audit entry kept for it.
    private async Task<string?> AuditedAddressAsync(ApiFactory factory, string? forwardedFor)
    {
        var email = ApiCalls.NewEmail();
        var registered = await ApiCalls.RegisterAsync(factory.CreateClient(), ApiCalls.RegisterBody(email));
        registered.EnsureSuccessStatusCode();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password = "Wrong-Password-1!" }) };
        if (forwardedFor is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        }

        var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);

        await using var db = sql.NewContext();
        var user = await db.Users.AsNoTracking().SingleAsync(candidate => candidate.Email == email, TestContext.Current.CancellationToken);
        return await db.AuditLogs.AsNoTracking()
            .Where(log => log.ActorUserId == user.Id && log.Action == AuditActions.LoginFailed)
            .Select(log => log.IpAddress)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Behind_a_trusted_proxy_the_audit_entry_keeps_the_address_the_proxy_reports()
    {
        await using var factory = Factory(enabled: true);

        var address = await AuditedAddressAsync(factory, "203.0.113.9");

        Assert.Equal("203.0.113.9", address);
    }

    [Fact]
    public async Task Only_the_address_the_nearest_proxy_added_is_believed()
    {
        await using var factory = Factory(enabled: true);

        var address = await AuditedAddressAsync(factory, "1.1.1.1, 203.0.113.9");

        Assert.Equal("203.0.113.9", address);
    }

    [Fact]
    public async Task A_header_that_is_not_an_address_is_ignored()
    {
        await using var factory = Factory(enabled: true);

        var address = await AuditedAddressAsync(factory, "not-an-address");

        Assert.Equal(FakeRemoteIpStartupFilter.Address, address);
    }

    [Fact]
    public async Task With_forwarding_off_the_header_is_ignored()
    {
        await using var factory = Factory(enabled: false);

        var address = await AuditedAddressAsync(factory, "203.0.113.9");

        Assert.Equal(FakeRemoteIpStartupFilter.Address, address);
    }

    [Fact]
    public async Task With_no_header_the_connection_address_is_kept_either_way()
    {
        await using var on = Factory(enabled: true);
        await using var off = Factory(enabled: false);

        Assert.Equal(FakeRemoteIpStartupFilter.Address, await AuditedAddressAsync(on, null));
        Assert.Equal(FakeRemoteIpStartupFilter.Address, await AuditedAddressAsync(off, null));
    }
}
