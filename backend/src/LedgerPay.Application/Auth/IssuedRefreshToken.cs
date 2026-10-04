namespace LedgerPay.Application.Auth;

// The refresh token as the browser gets it. It is not part of any JSON body: the API sends it as a cookie.
public sealed record IssuedRefreshToken(string Token, DateTime ExpiresAt);

public sealed record SignedIn(LoginResponse Response, IssuedRefreshToken Refresh);

// Refresh is null when another request replaced the token a moment ago. The browser already holds the new one.
public sealed record Refreshed(LoginResponse Response, IssuedRefreshToken? Refresh);
