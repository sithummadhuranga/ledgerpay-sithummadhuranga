using LedgerPay.Domain.Rules;

namespace LedgerPay.UnitTests.Rules;

public class NameMaskTests
{
    [Theory]
    [InlineData("Nimali Perera", "N*** P***")]
    [InlineData("Kasun Jayawardena Mudiyanselage", "K*** J*** M***")]
    [InlineData("Madonna", "M***")]
    [InlineData("Anne-Marie", "A***")]
    [InlineData("Conor O'Neil", "C*** O***")]
    [InlineData("  Nimali   Perera  ", "N*** P***")]
    public void Each_word_keeps_its_first_letter_and_nothing_else(string name, string expected)
    {
        Assert.Equal(expected, NameMask.Of(name));
    }

    [Fact]
    public void A_first_letter_with_its_vowel_signs_is_kept_whole()
    {
        // The first letter of a Sinhala word is a letter plus a sign. Cutting between them would leave a broken mark.
        Assert.Equal("නි*** පෙ***", NameMask.Of("නිමලි පෙරේරා"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_name_with_no_letters_masks_to_nothing(string name)
    {
        Assert.Equal(string.Empty, NameMask.Of(name));
    }

    [Fact]
    public void The_mask_does_not_show_how_long_a_word_is()
    {
        Assert.Equal(NameMask.Of("Li").Length, NameMask.Of("Jayawardena").Length);
    }
}
