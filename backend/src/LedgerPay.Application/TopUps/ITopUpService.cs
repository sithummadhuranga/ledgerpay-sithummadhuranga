using LedgerPay.Application.Common;

namespace LedgerPay.Application.TopUps;

public interface ITopUpService
{
    Task<ServiceResult<TopUpResponse>> TopUpAsync(
        Guid operatorUserId, string? idempotencyKey, TopUpRequest request, RequestInfo info, CancellationToken cancellationToken);
}
