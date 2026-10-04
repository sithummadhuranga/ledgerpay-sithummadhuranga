using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using LedgerPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Support;

internal static class TestData
{
    // A counter, not a random number, so two customers in one run can never share a phone. The seeded phones start with other digits.
    private static long nextPhone = 10_000_000;

    public static async Task<(User User, Wallet Wallet, LedgerAccount Account)> AddCustomerAsync(AppDbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var user = new User
        {
            Email = $"customer.{suffix}@example.com",
            Phone = "+947" + Interlocked.Increment(ref nextPhone),
            FullName = "Nimali Perera",
            PasswordHash = "hash-not-used-in-these-tests",
            CreatedAt = DateTime.UtcNow
        };
        var wallet = Wallet.Open(user, DateTime.UtcNow);
        var account = LedgerAccount.ForWallet(wallet);

        db.Add(account);
        await db.SaveChangesAsync();
        return (user, wallet, account);
    }

    // An operator or an admin made for one test, with a password the test knows, so a test can restrict it
    // without touching the seeded accounts that every other test signs in with.
    public static async Task<User> AddStaffAsync(AppDbContext db, string roleName, string password)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var role = await db.Roles.SingleAsync(candidate => candidate.Name == roleName);
        var user = new User
        {
            Email = $"{roleName.ToLowerInvariant()}.{suffix}@example.com",
            Phone = "+947" + Interlocked.Increment(ref nextPhone),
            FullName = $"Test {roleName} {suffix}",
            PasswordHash = new LedgerPay.Infrastructure.Security.PasswordService().Hash(password),
            CreatedAt = DateTime.UtcNow
        };
        user.UserRoles.Add(new UserRole { User = user, Role = role });

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<Transaction> AddTransactionAsync(
        AppDbContext db,
        Guid initiatedByUserId,
        TransactionType type = TransactionType.TopUp,
        TransactionStatus status = TransactionStatus.Completed,
        string? bankReference = null)
    {
        var transaction = new Transaction
        {
            Reference = "TX" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
            Type = type,
            Status = status,
            FailureCode = status == TransactionStatus.Failed ? "WALLET_FROZEN" : null,
            Amount = 1000.00m,
            BankReference = bankReference,
            InitiatedByUserId = initiatedByUserId,
            CreatedAt = DateTime.UtcNow
        };

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        return transaction;
    }

    public static async Task<LedgerEntry> AddEntryAsync(
        AppDbContext db,
        Guid transactionId,
        Guid accountId,
        decimal debit,
        decimal credit,
        DateTime? createdAt = null)
    {
        var entry = new LedgerEntry
        {
            TransactionId = transactionId,
            LedgerAccountId = accountId,
            Debit = debit,
            Credit = credit,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    // Funds a wallet the way a top-up would: Dr settlement float, Cr the wallet, and the balance moves with it.
    public static async Task FundAsync(AppDbContext db, Wallet wallet, decimal amount)
    {
        var settlement = await db.LedgerAccounts.SingleAsync(account => account.Code == LedgerAccountCodes.SettlementFloat);
        var walletAccount = await db.LedgerAccounts.SingleAsync(account => account.WalletId == wallet.Id);
        var transaction = await AddTransactionAsync(
            db, wallet.UserId, bankReference: "BANK" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant());

        await AddEntryAsync(db, transaction.Id, settlement.Id, amount, 0m);
        await AddEntryAsync(db, transaction.Id, walletAccount.Id, 0m, amount);

        var tracked = await db.Wallets.SingleAsync(candidate => candidate.Id == wallet.Id);
        tracked.Balance += amount;
        await db.SaveChangesAsync();
    }

    public static async Task<decimal> LedgerBalanceAsync(AppDbContext db, Wallet wallet)
    {
        var accountId = await db.LedgerAccounts.Where(account => account.WalletId == wallet.Id)
            .Select(account => account.Id).SingleAsync();
        var entries = await db.LedgerEntries.AsNoTracking().Where(entry => entry.LedgerAccountId == accountId).ToListAsync();
        return entries.Sum(entry => entry.Credit - entry.Debit);
    }

    public static async Task<SqlErrorText> CaptureSqlErrorAsync(Func<Task> action)
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(action);
        var root = exception;
        while (root.InnerException is not null)
        {
            root = root.InnerException;
        }

        return new SqlErrorText(root.Message);
    }

    public static async Task ExecuteAsync(AppDbContext db, FormattableString sql) =>
        await db.Database.ExecuteSqlInterpolatedAsync(sql);
}
