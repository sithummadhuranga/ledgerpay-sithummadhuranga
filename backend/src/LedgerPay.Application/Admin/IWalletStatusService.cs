using LedgerPay.Application.Common;

namespace LedgerPay.Application.Admin;

public interface IWalletStatusService
{
    Task<ServiceResult<WalletStatusResponse>> SetStatusAsync(
        Guid actorUserId, string walletNumber, WalletStatusRequest request, RequestInfo info, CancellationToken cancellationToken);
}
