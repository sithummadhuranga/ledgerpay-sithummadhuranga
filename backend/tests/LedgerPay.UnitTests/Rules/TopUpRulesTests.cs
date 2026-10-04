using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using LedgerPay.Domain.Rules;

namespace LedgerPay.UnitTests.Rules;

public class TopUpRulesTests
{
    private readonly LedgerSettings settings = TestSettings.FromAssignment();

    private static Wallet NewWallet(decimal balance = 0m, WalletStatus status = WalletStatus.Active) =>
        new() { Id = Guid.NewGuid(), Balance = balance, Status = status };

    private string? Evaluate(Wallet? wallet, decimal amount = 1000.00m, bool bankReferenceUsed = false) =>
        TopUpRules.FirstFailure(wallet, bankReferenceUsed, amount, settings);

    [Fact]
    public void Valid_top_up_has_no_failure()
    {
        Assert.Null(Evaluate(NewWallet()));
    }

    [Fact]
    public void Missing_wallet_is_reported_first()
    {
        Assert.Equal(ErrorCodes.WalletNotFound, Evaluate(null, amount: 5_000_000.00m, bankReferenceUsed: true));
    }

    [Fact]
    public void Frozen_wallet_cannot_be_topped_up()
    {
        Assert.Equal(ErrorCodes.WalletFrozen, Evaluate(NewWallet(status: WalletStatus.Frozen)));
    }

    [Fact]
    public void Frozen_wallet_is_reported_before_a_duplicate_bank_reference()
    {
        Assert.Equal(ErrorCodes.WalletFrozen,
            Evaluate(NewWallet(status: WalletStatus.Frozen), bankReferenceUsed: true));
    }

    [Fact]
    public void Bank_reference_that_a_completed_top_up_already_used_is_rejected()
    {
        Assert.Equal(ErrorCodes.DuplicateBankReference, Evaluate(NewWallet(), bankReferenceUsed: true));
    }

    [Fact]
    public void Duplicate_bank_reference_is_reported_before_the_balance_cap()
    {
        Assert.Equal(ErrorCodes.DuplicateBankReference,
            Evaluate(NewWallet(1_999_999.00m), amount: 100.00m, bankReferenceUsed: true));
    }

    [Fact]
    public void Top_up_that_would_pass_the_balance_cap_is_rejected()
    {
        Assert.Equal(ErrorCodes.BalanceLimitExceeded, Evaluate(NewWallet(1_999_000.00m), amount: 1_000.01m));
    }

    [Fact]
    public void Top_up_ending_exactly_on_the_cap_is_allowed()
    {
        Assert.Null(Evaluate(NewWallet(1_999_000.00m), amount: 1_000.00m));
    }

    [Fact]
    public void There_is_no_minimum_amount_beyond_above_zero()
    {
        Assert.Null(Evaluate(NewWallet(), amount: 0.01m));
    }
}
