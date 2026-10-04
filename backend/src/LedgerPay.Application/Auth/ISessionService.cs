using LedgerPay.Application.Common;

namespace LedgerPay.Application.Auth;

public interface ISessionService
{
    // Adds the first refresh token of a new sign-in to the context. The caller saves, in the same transaction as the sign-in.
    IssuedRefreshToken Start(Guid userId, DateTime now, RequestInfo info);

    Task<ServiceResult<Refreshed>> RefreshAsync(string? token, RequestInfo info, CancellationToken cancellationToken);

    // Ends the session the token belongs to. A token that is missing or unknown is not an error: the caller is signed out anyway.
    Task EndAsync(string? token, RequestInfo info, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionResponse>> ListAsync(Guid userId, string? currentToken, CancellationToken cancellationToken);

    // True in the answer when the session that was ended is the one the caller is using.
    Task<ServiceResult<bool>> RevokeAsync(
        Guid userId, Guid sessionId, string? currentToken, RequestInfo info, CancellationToken cancellationToken);
}
