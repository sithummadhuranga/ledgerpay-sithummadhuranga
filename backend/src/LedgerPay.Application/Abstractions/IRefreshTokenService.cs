namespace LedgerPay.Application.Abstractions;

public interface IRefreshTokenService
{
    // A new random token. It goes to the browser once and is never stored.
    string NewToken();

    // What is stored and looked up instead of the token.
    string Hash(string token);
}
