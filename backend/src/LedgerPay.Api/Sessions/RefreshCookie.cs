using LedgerPay.Application.Auth;

namespace LedgerPay.Api.Sessions;

// The refresh token travels only in this cookie. JavaScript cannot read it, it goes nowhere but the auth routes,
// and the browser never sends it with a request that started on another site.
public sealed class RefreshCookie(RefreshCookieOptions options)
{
    public const string Name = "ledgerpay_refresh";
    public const string Path = "/api/v1/auth";

    public string? Read(HttpRequest request) => request.Cookies.TryGetValue(Name, out var value) ? value : null;

    public void Set(HttpResponse response, IssuedRefreshToken token)
    {
        var cookie = Options();
        cookie.Expires = new DateTimeOffset(DateTime.SpecifyKind(token.ExpiresAt, DateTimeKind.Utc));
        response.Cookies.Append(Name, token.Token, cookie);
    }

    public void Clear(HttpResponse response) => response.Cookies.Delete(Name, Options());

    private CookieOptions Options() => new()
    {
        HttpOnly = true,
        Secure = options.CookieSecure,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        IsEssential = true
    };
}
