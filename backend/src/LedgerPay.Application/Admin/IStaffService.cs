using LedgerPay.Application.Common;

namespace LedgerPay.Application.Admin;

public interface IStaffService
{
    Task<IReadOnlyList<StaffMember>> ListAsync(CancellationToken cancellationToken);

    // Restricts or releases an operator account. Only an operator can be restricted: not a customer, not an admin.
    Task<ServiceResult<StaffMember>> SetRestrictionAsync(
        Guid actorUserId, StaffRestrictionRequest request, RequestInfo info, CancellationToken cancellationToken);
}
