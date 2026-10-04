using LedgerPay.Application.Common;

namespace LedgerPay.Application.Transactions;

public interface ITransactionQueries
{
    Task<ServiceResult<PagedResponse<HistoryItem>>> HistoryAsync(Guid userId, HistoryQuery query, CancellationToken cancellationToken);

    Task<ServiceResult<TransactionResponse>> GetByReferenceAsync(
        Guid userId, bool isBackOffice, string reference, CancellationToken cancellationToken);
}
