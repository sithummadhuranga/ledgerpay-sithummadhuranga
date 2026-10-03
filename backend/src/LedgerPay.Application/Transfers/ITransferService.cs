using LedgerPay.Application.Common;

namespace LedgerPay.Application.Transfers;

public interface ITransferService
{
    Task<ServiceResult<QuoteResponse>> QuoteAsync(decimal amount, CancellationToken cancellationToken);

    Task<ServiceResult<TransferResponse>> TransferAsync(
        Guid userId, TransferRequest request, RequestInfo info, CancellationToken cancellationToken);
}
