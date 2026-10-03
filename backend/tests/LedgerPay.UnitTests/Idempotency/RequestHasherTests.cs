using LedgerPay.Application.Idempotency;
using LedgerPay.Application.TopUps;
using LedgerPay.Application.Transfers;

namespace LedgerPay.UnitTests.Idempotency;

public class RequestHasherTests
{
    private static TransferRequest Transfer(
        string? number = "123456789012", string? phone = null, decimal amount = 1000.00m, string? note = null) =>
        new(number, phone, amount, note);

    private static TopUpRequest TopUp(
        string wallet = "123456789012", decimal amount = 1000.00m, string reference = "BANK123456", string? note = null) =>
        new(wallet, amount, reference, note);

    [Fact]
    public void The_hash_is_64_lowercase_hex_characters()
    {
        Assert.Matches("^[0-9a-f]{64}$", RequestHasher.Hash(Transfer()));
        Assert.Matches("^[0-9a-f]{64}$", RequestHasher.Hash(TopUp()));
    }

    [Fact]
    public void The_same_transfer_always_gives_the_same_hash()
    {
        Assert.Equal(RequestHasher.Hash(Transfer()), RequestHasher.Hash(Transfer()));
    }

    [Fact]
    public void The_scale_of_the_amount_does_not_change_the_hash()
    {
        Assert.Equal(RequestHasher.Hash(Transfer(amount: 1000m)), RequestHasher.Hash(Transfer(amount: 1000.00m)));
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("number")]
    [InlineData("phone")]
    [InlineData("note")]
    public void Changing_any_transfer_field_changes_the_hash(string field)
    {
        var changed = field switch
        {
            "amount" => Transfer(amount: 1000.01m),
            "number" => Transfer(number: "123456789013"),
            "phone" => Transfer(number: null, phone: "+94771234567"),
            _ => Transfer(note: "Rent")
        };

        Assert.NotEqual(RequestHasher.Hash(Transfer()), RequestHasher.Hash(changed));
    }

    [Fact]
    public void A_missing_note_and_an_empty_note_are_the_same_request()
    {
        Assert.Equal(RequestHasher.Hash(Transfer(note: null)), RequestHasher.Hash(Transfer(note: "")));
    }

    [Fact]
    public void Fields_cannot_be_shifted_into_each_other_to_forge_a_matching_hash()
    {
        var first = Transfer(number: null, phone: "+94771234567", note: "x");
        var second = Transfer(number: null, phone: "+94771234567x", note: "");

        Assert.NotEqual(RequestHasher.Hash(first), RequestHasher.Hash(second));
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("wallet")]
    [InlineData("reference")]
    [InlineData("note")]
    public void Changing_any_top_up_field_changes_the_hash(string field)
    {
        var changed = field switch
        {
            "amount" => TopUp(amount: 1000.01m),
            "wallet" => TopUp(wallet: "123456789013"),
            "reference" => TopUp(reference: "BANK654321"),
            _ => TopUp(note: "Branch deposit")
        };

        Assert.NotEqual(RequestHasher.Hash(TopUp()), RequestHasher.Hash(changed));
    }

    [Fact]
    public void The_bank_reference_hashes_the_same_in_any_letter_case()
    {
        Assert.Equal(RequestHasher.Hash(TopUp(reference: "bank123456")), RequestHasher.Hash(TopUp(reference: "BANK123456")));
    }

    [Fact]
    public void A_transfer_and_a_top_up_with_matching_values_do_not_share_a_hash()
    {
        var transfer = new TransferRequest("123456789012", null, 1000.00m, null);
        var topUp = new TopUpRequest("123456789012", 1000.00m, "", null);

        Assert.NotEqual(RequestHasher.Hash(transfer), RequestHasher.Hash(topUp));
    }
}
