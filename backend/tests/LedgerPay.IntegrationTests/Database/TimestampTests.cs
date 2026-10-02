using LedgerPay.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Database;

public class TimestampTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Timestamps_are_read_back_as_utc()
    {
        await using var writer = sql.NewContext();
        var (user, wallet, _) = await TestData.AddCustomerAsync(writer);
        wallet.Status = WalletStatus.Frozen;
        wallet.StatusChangedAt = DateTime.UtcNow;
        await writer.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var reader = sql.NewContext();
        var loaded = await reader.Wallets.AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == user.Id, TestContext.Current.CancellationToken);

        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, loaded.StatusChangedAt!.Value.Kind);
    }

    [Fact]
    public async Task A_local_time_is_stored_as_the_same_instant_in_utc()
    {
        var instant = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        await using var writer = sql.NewContext();
        var (user, wallet, _) = await TestData.AddCustomerAsync(writer);
        wallet.Status = WalletStatus.Frozen;
        wallet.StatusChangedAt = instant.ToLocalTime();
        await writer.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var reader = sql.NewContext();
        var loaded = await reader.Wallets.AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == user.Id, TestContext.Current.CancellationToken);

        Assert.Equal(instant, loaded.StatusChangedAt);
    }
}
