using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using LedgerPay.Domain.Enums;
using LedgerPay.Domain.Rules;

namespace LedgerPay.UnitTests.Rules;

public class TransferRulesTests
{
    private readonly LedgerSettings settings = TestSettings.FromAssignment();

    private static Wallet NewWallet(decimal balance, WalletStatus status = WalletStatus.Active) =>
        new() { Id = Guid.NewGuid(), Balance = balance, Status = status };

    private string? Evaluate(Wallet sender, Wallet? receiver, decimal amount, decimal fee = 10.00m) =>
        TransferRules.FirstFailure(sender, receiver, amount, fee, settings);

    [Fact]
    public void Valid_transfer_has_no_failure()
    {
        Assert.Null(Evaluate(NewWallet(5000.00m), NewWallet(0m), 1000.00m));
    }

    [Fact]
    public void Missing_recipient_is_reported_first()
    {
        Assert.Equal(ErrorCodes.RecipientNotFound, Evaluate(NewWallet(0m, WalletStatus.Frozen), null, 5.00m));
    }

    [Fact]
    public void Transfer_to_yourself_is_rejected()
    {
        var wallet = NewWallet(5000.00m);

        Assert.Equal(ErrorCodes.SelfTransferNotAllowed, Evaluate(wallet, wallet, 1000.00m));
    }

    [Fact]
    public void Self_transfer_is_reported_before_the_amount_checks()
    {
        var wallet = NewWallet(5000.00m);

        Assert.Equal(ErrorCodes.SelfTransferNotAllowed, Evaluate(wallet, wallet, 5.00m));
    }

    [Theory]
    [InlineData("99.99")]
    [InlineData("0.01")]
    public void Amount_below_the_minimum_is_rejected(string amount)
    {
        Assert.Equal(ErrorCodes.AmountBelowMinimum, Evaluate(NewWallet(5000.00m), NewWallet(0m), decimal.Parse(amount)));
    }

    [Fact]
    public void Amount_equal_to_the_minimum_is_accepted()
    {
        Assert.Null(Evaluate(NewWallet(5000.00m), NewWallet(0m), 100.00m));
    }

    [Fact]
    public void Amount_above_the_maximum_is_rejected()
    {
        Assert.Equal(ErrorCodes.AmountAboveMaximum, Evaluate(NewWallet(900_000.00m), NewWallet(0m), 500_000.01m, 250.00m));
    }

    [Fact]
    public void Amount_equal_to_the_maximum_is_accepted()
    {
        Assert.Null(Evaluate(NewWallet(900_000.00m), NewWallet(0m), 500_000.00m, 250.00m));
    }

    [Fact]
    public void Frozen_sender_cannot_send()
    {
        Assert.Equal(ErrorCodes.WalletFrozen, Evaluate(NewWallet(5000.00m, WalletStatus.Frozen), NewWallet(0m), 1000.00m));
    }

    [Fact]
    public void Frozen_recipient_cannot_receive()
    {
        Assert.Equal(ErrorCodes.WalletFrozen, Evaluate(NewWallet(5000.00m), NewWallet(0m, WalletStatus.Frozen), 1000.00m));
    }

    [Fact]
    public void Amount_limits_are_checked_before_the_frozen_wallets()
    {
        Assert.Equal(ErrorCodes.AmountBelowMinimum,
            Evaluate(NewWallet(5000.00m, WalletStatus.Frozen), NewWallet(0m), 50.00m));
    }

    [Fact]
    public void Frozen_sender_is_reported_before_insufficient_funds()
    {
        Assert.Equal(ErrorCodes.WalletFrozen, Evaluate(NewWallet(0m, WalletStatus.Frozen), NewWallet(0m), 1000.00m));
    }

    [Fact]
    public void Balance_that_covers_the_amount_but_not_the_fee_is_insufficient()
    {
        Assert.Equal(ErrorCodes.InsufficientFunds, Evaluate(NewWallet(1000.00m), NewWallet(0m), 1000.00m, 10.00m));
    }

    [Fact]
    public void Balance_that_covers_the_amount_and_the_fee_exactly_is_enough()
    {
        Assert.Null(Evaluate(NewWallet(1010.00m), NewWallet(0m), 1000.00m, 10.00m));
    }

    [Fact]
    public void Recipient_balance_would_pass_the_cap()
    {
        Assert.Equal(ErrorCodes.ReceiverBalanceLimitExceeded,
            Evaluate(NewWallet(5000.00m), NewWallet(1_999_900.01m), 100.00m));
    }

    [Fact]
    public void Recipient_balance_ending_exactly_on_the_cap_is_allowed()
    {
        Assert.Null(Evaluate(NewWallet(5000.00m), NewWallet(1_999_900.00m), 100.00m));
    }

    [Fact]
    public void Insufficient_funds_is_reported_before_the_recipient_cap()
    {
        Assert.Equal(ErrorCodes.InsufficientFunds, Evaluate(NewWallet(50.00m), NewWallet(1_999_999.00m), 1000.00m));
    }

    [Fact]
    public void Amount_above_the_maximum_is_reported_before_the_frozen_wallets_and_the_balance()
    {
        Assert.Equal(ErrorCodes.AmountAboveMaximum,
            Evaluate(NewWallet(0m, WalletStatus.Frozen), NewWallet(0m), 500_000.01m));
    }

    [Fact]
    public void Frozen_recipient_is_reported_before_insufficient_funds()
    {
        Assert.Equal(ErrorCodes.WalletFrozen, Evaluate(NewWallet(0m), NewWallet(0m, WalletStatus.Frozen), 1000.00m));
    }

    [Fact]
    public void Amount_below_the_minimum_is_reported_before_insufficient_funds()
    {
        Assert.Equal(ErrorCodes.AmountBelowMinimum, Evaluate(NewWallet(0m), NewWallet(0m), 50.00m));
    }
}
