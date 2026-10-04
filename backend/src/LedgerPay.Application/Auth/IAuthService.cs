using LedgerPay.Application.Common;

namespace LedgerPay.Application.Auth;

public interface IAuthService
{
    Task<ServiceResult<RegisterResponse>> RegisterAsync(RegisterRequest request, RequestInfo info, CancellationToken cancellationToken);

    Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, RequestInfo info, CancellationToken cancellationToken);
}
