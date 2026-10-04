namespace LedgerPay.Application.Abstractions;

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken Create(Guid userId, IReadOnlyCollection<string> roles, DateTime now);
}
