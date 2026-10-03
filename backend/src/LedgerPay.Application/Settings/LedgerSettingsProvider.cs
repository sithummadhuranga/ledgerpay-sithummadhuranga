using LedgerPay.Application.Abstractions;
using LedgerPay.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Application.Settings;

// Reads the fee and limits on every call. There is no cache, so a changed setting applies to the next request.
public sealed class LedgerSettingsProvider(IAppDbContext db)
{
    public async Task<LedgerSettings> GetAsync(CancellationToken cancellationToken)
    {
        var values = await db.SystemSettings.AsNoTracking()
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

        return LedgerSettings.From(values);
    }
}
