using LedgerPay.Application.Common;
using LedgerPay.Application.Transactions;

namespace LedgerPay.Application.BackOffice;

public interface IBackOfficeQueries
{
    Task<PagedResponse<UserSummary>> ListUsersAsync(UserSearchQuery query, CancellationToken cancellationToken);

    // Looking at a person is written to the audit log with who looked, so reading is not invisible.
    Task<ServiceResult<UserDetail>> GetUserAsync(Guid actorUserId, string walletNumber, RequestInfo info, CancellationToken cancellationToken);

    Task<PagedResponse<StaffTransactionItem>> ListTransactionsAsync(StaffTransactionQuery query, CancellationToken cancellationToken);

    Task<PagedResponse<AuditItem>> ListAuditAsync(AuditQuery query, CancellationToken cancellationToken);
}
