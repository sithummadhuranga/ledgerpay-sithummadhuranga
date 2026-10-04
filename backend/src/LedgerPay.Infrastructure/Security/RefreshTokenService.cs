using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using LedgerPay.Application.Abstractions;
using LedgerPay.Domain.Rules;

namespace LedgerPay.Infrastructure.Security;

public sealed class RefreshTokenService : IRefreshTokenService
{
    public string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SessionRules.TokenBytes));

    // The token is 256 random bits, so a plain SHA-256 is enough: there is nothing to guess and nothing to slow down.
    public string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
}
