using System.Security.Cryptography;

namespace LedgerPay.Domain.Identifiers;

public static class WalletNumberGenerator
{
    public const int Length = 12;

    // Random, not sequential, so a wallet number cannot be guessed from another one.
    public static string Next()
    {
        Span<char> digits = stackalloc char[Length];
        digits[0] = (char)('1' + RandomNumberGenerator.GetInt32(9));
        for (var i = 1; i < Length; i++)
        {
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }

        return new string(digits);
    }
}
