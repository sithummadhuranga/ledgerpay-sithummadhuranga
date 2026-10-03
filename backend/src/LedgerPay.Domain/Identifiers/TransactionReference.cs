using System.Security.Cryptography;

namespace LedgerPay.Domain.Identifiers;

public static class TransactionReference
{
    // Crockford base32: no I, L, O or U, so a reference read out over the phone is hard to get wrong.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int RandomLength = 14;

    public static string Next()
    {
        Span<char> chars = stackalloc char[RandomLength];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return "TX" + new string(chars);
    }
}
