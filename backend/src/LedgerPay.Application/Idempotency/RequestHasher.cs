using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LedgerPay.Application.Transfers;

namespace LedgerPay.Application.Idempotency;

// A fingerprint of the validated request in a fixed form, so spacing and property order cannot change it.
// Two requests with one key are the same request only when their fingerprints match.
public static class RequestHasher
{
    public static string Hash(TransferRequest request) => Sha256(
        "transfer", request.RecipientWalletNumber, request.RecipientPhone, Amount(request.Amount), request.Note);

    private static string Amount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    // Every part is written with its length in front, so text moved from one field into the next gives a different hash.
    private static string Sha256(params string?[] parts)
    {
        var canonical = new StringBuilder();
        foreach (var part in parts)
        {
            var text = part ?? string.Empty;
            canonical.Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(text).Append('|');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}
