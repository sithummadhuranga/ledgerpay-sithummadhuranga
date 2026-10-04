using LedgerPay.Application.Common;

namespace LedgerPay.Application.Wallets;

public interface IWalletService
{
    Task<ServiceResult<WalletResponse>> GetMineAsync(Guid userId, CancellationToken cancellationToken);

    Task<ServiceResult<LookupResponse>> LookupAsync(LookupQuery query, CancellationToken cancellationToken);
}
