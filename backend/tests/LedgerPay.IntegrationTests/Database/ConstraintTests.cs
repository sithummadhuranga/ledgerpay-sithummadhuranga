using LedgerPay.Domain.Enums;
using LedgerPay.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Database;

public class ConstraintTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Entry_with_both_debit_and_credit_is_rejected()
    {
        await using var db = sql.NewContext();
        var (user, _, account) = await TestData.AddCustomerAsync(db);
        var transaction = await TestData.AddTransactionAsync(db, user.Id);

        var error = await TestData.CaptureSqlErrorAsync(() =>
            TestData.AddEntryAsync(db, transaction.Id, account.Id, 100.00m, 100.00m));

        Assert.Contains("CK_LedgerEntries_ExactlyOneSide", error.Message);
    }

    [Fact]
    public async Task Entry_with_neither_debit_nor_credit_is_rejected()
    {
        await using var db = sql.NewContext();
        var (user, _, account) = await TestData.AddCustomerAsync(db);
        var transaction = await TestData.AddTransactionAsync(db, user.Id);

        var error = await TestData.CaptureSqlErrorAsync(() =>
            TestData.AddEntryAsync(db, transaction.Id, account.Id, 0m, 0m));

        Assert.Contains("CK_LedgerEntries_ExactlyOneSide", error.Message);
    }

    [Fact]
    public async Task Entry_with_a_negative_amount_is_rejected()
    {
        await using var db = sql.NewContext();
        var (user, _, account) = await TestData.AddCustomerAsync(db);
        var transaction = await TestData.AddTransactionAsync(db, user.Id);

        var error = await TestData.CaptureSqlErrorAsync(() =>
            TestData.AddEntryAsync(db, transaction.Id, account.Id, -5.00m, 0m));

        // A negative amount also breaks the one-side rule, and SQL Server reports whichever it checks first.
        Assert.True(
            error.Message.Contains("CK_LedgerEntries_Amounts_NonNegative") ||
            error.Message.Contains("CK_LedgerEntries_ExactlyOneSide"));
    }

    [Fact]
    public async Task Wallet_balance_cannot_go_negative()
    {
        await using var db = sql.NewContext();
        var (_, wallet, _) = await TestData.AddCustomerAsync(db);
        wallet.Balance = -0.01m;

        var error = await TestData.CaptureSqlErrorAsync(() => db.SaveChangesAsync());

        Assert.Contains("CK_Wallets_Balance_NonNegative", error.Message);
    }

    [Theory]
    [InlineData("12345678901")]
    [InlineData("12345678901A")]
    public async Task Wallet_number_must_be_exactly_twelve_digits(string walletNumber)
    {
        await using var db = sql.NewContext();
        var (_, wallet, _) = await TestData.AddCustomerAsync(db);
        wallet.WalletNumber = walletNumber;

        var error = await TestData.CaptureSqlErrorAsync(() => db.SaveChangesAsync());

        Assert.Contains("CK_Wallets_WalletNumber_TwelveDigits", error.Message);
    }

    [Fact]
    public async Task Wallet_number_longer_than_twelve_digits_is_rejected()
    {
        await using var db = sql.NewContext();
        var (_, wallet, _) = await TestData.AddCustomerAsync(db);
        wallet.WalletNumber = "1234567890123";

        var error = await TestData.CaptureSqlErrorAsync(() => db.SaveChangesAsync());

        Assert.Contains("truncated", error.Message);
    }

    [Fact]
    public async Task Transaction_amount_must_be_above_zero()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        var transaction = await TestData.AddTransactionAsync(db, user.Id);
        transaction.Amount = 0m;

        var error = await TestData.CaptureSqlErrorAsync(() => db.SaveChangesAsync());

        Assert.Contains("CK_Transactions_Amount_Positive", error.Message);
    }

    [Fact]
    public async Task Failed_transaction_must_carry_a_failure_code()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        var transaction = await TestData.AddTransactionAsync(db, user.Id, status: TransactionStatus.Failed);
        transaction.FailureCode = null;

        var error = await TestData.CaptureSqlErrorAsync(() => db.SaveChangesAsync());

        Assert.Contains("CK_Transactions_FailureCode_OnlyWhenFailed", error.Message);
    }

    [Fact]
    public async Task Completed_transaction_must_not_carry_a_failure_code()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        var transaction = await TestData.AddTransactionAsync(db, user.Id);
        transaction.FailureCode = "WALLET_FROZEN";

        var error = await TestData.CaptureSqlErrorAsync(() => db.SaveChangesAsync());

        Assert.Contains("CK_Transactions_FailureCode_OnlyWhenFailed", error.Message);
    }

    [Fact]
    public async Task Email_must_be_unique()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        var (other, _, _) = await TestData.AddCustomerAsync(db);
        other.Email = user.Email;

        var error = await TestData.CaptureSqlErrorAsync(() => db.SaveChangesAsync());

        Assert.Contains("IX_Users_Email", error.Message);
    }

    [Fact]
    public async Task Email_must_be_stored_in_lowercase()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        user.Email = user.Email.ToUpperInvariant();

        var error = await TestData.CaptureSqlErrorAsync(() => db.SaveChangesAsync());

        Assert.Contains("CK_Users_Email_Lowercase", error.Message);
    }

    [Fact]
    public async Task Two_completed_top_ups_cannot_share_a_bank_reference()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        var reference = "BANK" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        await TestData.AddTransactionAsync(db, user.Id, bankReference: reference);

        var error = await TestData.CaptureSqlErrorAsync(() =>
            TestData.AddTransactionAsync(db, user.Id, bankReference: reference));

        Assert.Contains("IX_Transactions_BankReference", error.Message);
    }

    [Fact]
    public async Task A_failed_top_up_can_reuse_a_bank_reference()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        var reference = "BANK" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        await TestData.AddTransactionAsync(db, user.Id, status: TransactionStatus.Failed, bankReference: reference);
        await TestData.AddTransactionAsync(db, user.Id, status: TransactionStatus.Failed, bankReference: reference);

        await TestData.AddTransactionAsync(db, user.Id, bankReference: reference);

        var count = await db.Transactions.CountAsync(transaction => transaction.BankReference == reference, TestContext.Current.CancellationToken);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Idempotency_key_is_unique_per_user_and_endpoint()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        var key = Guid.NewGuid().ToString();
        await TestData.ExecuteAsync(db,
            $"INSERT INTO IdempotencyKeys (Id, UserId, [Key], Endpoint, RequestHash, CreatedAt) VALUES ({Guid.NewGuid()}, {user.Id}, {key}, 'POST /transfers', 'abc', SYSUTCDATETIME())");

        var error = await TestData.CaptureSqlErrorAsync(() =>
            TestData.ExecuteAsync(db,
                $"INSERT INTO IdempotencyKeys (Id, UserId, [Key], Endpoint, RequestHash, CreatedAt) VALUES ({Guid.NewGuid()}, {user.Id}, {key}, 'POST /transfers', 'abc', SYSUTCDATETIME())"));

        Assert.Contains("IX_IdempotencyKeys_UserId_Key_Endpoint", error.Message);
    }

    [Fact]
    public async Task Wallet_number_must_be_unique()
    {
        await using var db = sql.NewContext();
        var (_, wallet, _) = await TestData.AddCustomerAsync(db);
        var (_, other, _) = await TestData.AddCustomerAsync(db);
        other.WalletNumber = wallet.WalletNumber;

        var error = await TestData.CaptureSqlErrorAsync(() => db.SaveChangesAsync());

        Assert.Contains("IX_Wallets_WalletNumber", error.Message);
    }

    [Fact]
    public async Task Phone_must_be_unique()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        var (other, _, _) = await TestData.AddCustomerAsync(db);
        other.Phone = user.Phone;

        var error = await TestData.CaptureSqlErrorAsync(() => db.SaveChangesAsync());

        Assert.Contains("IX_Users_Phone", error.Message);
    }

    [Fact]
    public async Task Transaction_reference_must_be_unique()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        var first = await TestData.AddTransactionAsync(db, user.Id);
        var second = await TestData.AddTransactionAsync(db, user.Id);
        second.Reference = first.Reference;

        var error = await TestData.CaptureSqlErrorAsync(() => db.SaveChangesAsync());

        Assert.Contains("IX_Transactions_Reference", error.Message);
    }

    [Fact]
    public async Task Wallet_status_must_be_a_known_value()
    {
        await using var db = sql.NewContext();
        var (_, wallet, _) = await TestData.AddCustomerAsync(db);

        var error = await TestData.CaptureSqlErrorAsync(() =>
            TestData.ExecuteAsync(db, $"UPDATE Wallets SET Status = 'Closed' WHERE Id = {wallet.Id}"));

        Assert.Contains("CK_Wallets_Status_Valid", error.Message);
    }

    [Fact]
    public async Task Transaction_type_must_be_a_known_value()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        var transaction = await TestData.AddTransactionAsync(db, user.Id);

        var error = await TestData.CaptureSqlErrorAsync(() =>
            TestData.ExecuteAsync(db, $"UPDATE Transactions SET Type = 'Refund' WHERE Id = {transaction.Id}"));

        Assert.Contains("CK_Transactions_Type_Valid", error.Message);
    }

    [Fact]
    public async Task Transaction_status_must_be_a_known_value()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        var transaction = await TestData.AddTransactionAsync(db, user.Id);

        var error = await TestData.CaptureSqlErrorAsync(() =>
            TestData.ExecuteAsync(db, $"UPDATE Transactions SET Status = 'Pending' WHERE Id = {transaction.Id}"));

        // An unknown status also breaks the failure-code rule, and SQL Server reports whichever it checks first.
        Assert.True(
            error.Message.Contains("CK_Transactions_Status_Valid") ||
            error.Message.Contains("CK_Transactions_FailureCode_OnlyWhenFailed"));
    }

    [Fact]
    public async Task Ledger_account_type_must_be_a_known_value()
    {
        await using var db = sql.NewContext();
        var (_, _, account) = await TestData.AddCustomerAsync(db);

        var error = await TestData.CaptureSqlErrorAsync(() =>
            TestData.ExecuteAsync(db, $"UPDATE LedgerAccounts SET Type = 'Equity' WHERE Id = {account.Id}"));

        Assert.Contains("CK_LedgerAccounts_Type_Valid", error.Message);
    }

    [Fact]
    public async Task Transaction_fee_cannot_be_negative()
    {
        await using var db = sql.NewContext();
        var (user, _, _) = await TestData.AddCustomerAsync(db);
        var transaction = await TestData.AddTransactionAsync(db, user.Id);
        transaction.Fee = -0.01m;

        var error = await TestData.CaptureSqlErrorAsync(() => db.SaveChangesAsync());

        Assert.Contains("CK_Transactions_Fee_NonNegative", error.Message);
    }

    [Fact]
    public async Task Setting_value_cannot_be_negative()
    {
        await using var db = sql.NewContext();
        var key = "Test." + Guid.NewGuid().ToString("N");

        var error = await TestData.CaptureSqlErrorAsync(() =>
            TestData.ExecuteAsync(db,
                $"INSERT INTO SystemSettings (Id, [Key], Value, UpdatedAt) VALUES ({Guid.NewGuid()}, {key}, -1.00, SYSUTCDATETIME())"));

        Assert.Contains("CK_SystemSettings_Value_NonNegative", error.Message);
    }

    [Fact]
    public async Task Transaction_must_belong_to_an_existing_user()
    {
        await using var db = sql.NewContext();

        var error = await TestData.CaptureSqlErrorAsync(() =>
            TestData.AddTransactionAsync(db, Guid.NewGuid()));

        Assert.Contains("FOREIGN KEY", error.Message);
    }
}
