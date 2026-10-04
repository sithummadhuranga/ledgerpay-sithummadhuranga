using LedgerPay.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace LedgerPay.IntegrationTests.Api;

public class CorrelationIdMiddlewareTests
{
    private static async Task<string> TraceIdForAsync(string suppliedId)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = suppliedId;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        return context.TraceIdentifier;
    }

    [Theory]
    [InlineData("abc-123_DEF")]
    [InlineData("a")]
    public async Task A_safe_id_is_kept(string id)
    {
        Assert.Equal(id, await TraceIdForAsync(id));
    }

    [Theory]
    [InlineData("abc\n")]
    [InlineData("abc\r\ninjected: header")]
    [InlineData("with space")]
    [InlineData("<script>")]
    [InlineData("")]
    public async Task An_id_with_anything_else_in_it_is_replaced(string id)
    {
        var traceId = await TraceIdForAsync(id);

        Assert.NotEqual(id, traceId);
        Assert.Matches("^[A-Za-z0-9]{32}$", traceId);
    }

    [Fact]
    public async Task An_id_of_65_characters_is_replaced_and_64_is_kept()
    {
        Assert.Equal(new string('a', 64), await TraceIdForAsync(new string('a', 64)));
        Assert.NotEqual(new string('a', 65), await TraceIdForAsync(new string('a', 65)));
    }
}
