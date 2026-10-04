using LedgerPay.Domain.Rules;
using LedgerPay.Infrastructure.Security;

namespace LedgerPay.IntegrationTests.Auth;

public class RefreshTokenServiceTests
{
    private readonly RefreshTokenService service = new();

    [Fact]
    public void A_new_token_is_well_formed()
    {
        Assert.True(SessionRules.IsWellFormed(service.NewToken()));
    }

    [Fact]
    public void A_thousand_new_tokens_are_all_different()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => service.NewToken()).ToHashSet();

        Assert.Equal(1000, tokens.Count);
    }

    [Fact]
    public void The_hash_is_the_lower_case_hex_of_sha_256()
    {
        // The published SHA-256 test vector for "abc", so the algorithm cannot change unnoticed.
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", service.Hash("abc"));
    }

    [Fact]
    public void The_hash_is_64_characters_the_same_every_time_and_not_the_token()
    {
        var token = service.NewToken();

        Assert.Equal(64, service.Hash(token).Length);
        Assert.Equal(service.Hash(token), service.Hash(token));
        Assert.NotEqual(token, service.Hash(token));
        Assert.NotEqual(service.Hash(token), service.Hash(service.NewToken()));
    }
}
