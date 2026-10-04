namespace LedgerPay.Api.Sessions;

public sealed class RefreshCookieOptions
{
    public const string SectionName = "Sessions";

    // The cookie is sent over https only. It is switched off for local development over http and for the compose stack
    // on localhost, and must stay on wherever the site is served over https.
    public bool CookieSecure { get; set; } = true;
}
