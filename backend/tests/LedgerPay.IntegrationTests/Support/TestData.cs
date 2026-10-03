using System.Security.Cryptography;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using LedgerPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.IntegrationTests.Support;

internal static class TestData
{
    public static async Task<(User User, Wallet Wallet, LedgerAccount Account)> AddCustomerAsync(AppDbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var user = new User
        {
            Email = $"customer.{suffix}@example.com",
            Phone = "+947" + RandomNumberGenerator.GetInt32(10_000_000, 99_999_999),
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
