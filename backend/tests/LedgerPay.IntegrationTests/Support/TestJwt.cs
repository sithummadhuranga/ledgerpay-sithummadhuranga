using System.Security.Cryptography;
using LedgerPay.Infrastructure.Security;

namespace LedgerPay.IntegrationTests.Support;

internal static class TestJwt
{
    public const string Issuer = "ledgerpay-tests";
    public const string Audience = "ledgerpay-tests-clients";

    // 64 bytes, so a test can also sign with HS512 using the same key. A new random key for every run. It only ever signs tokens made by the tests.
    public static JwtOptions Options() => new()
    {
        SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
        Issuer = Issuer,
        Audience = Audience,
        AccessTokenMinutes = 15
    };
}
