using System.Globalization;

namespace LedgerPay.Domain.Rules;

public static class NameMask
{
    // Shows only the first letter of each word, for a name a stranger may see, such as the holder of a wallet
    // found by a lookup. The same number of stars for every word, so the length of a word is not given away.
    // A text element is used, not a char, because a Sinhala letter and its vowel sign must stay together.
    public static string Of(string fullName)
    {
        var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', words.Select(word => StringInfo.GetNextTextElement(word) + "***"));
    }
}
