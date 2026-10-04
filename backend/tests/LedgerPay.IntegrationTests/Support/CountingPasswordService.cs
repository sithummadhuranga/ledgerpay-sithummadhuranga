using LedgerPay.Application.Abstractions;
using LedgerPay.Infrastructure.Security;

namespace LedgerPay.IntegrationTests.Support;

// The real hasher, with a count of how often each method was called.
public sealed class CountingPasswordService : IPasswordService
{
    private readonly PasswordService inner = new();

    public int Hashes { get; private set; }

    public int Verifies { get; private set; }

    public int UnknownAccountChecks { get; private set; }

    public string Hash(string password)
    {
        Hashes++;
        return inner.Hash(password);
    }

    public bool Verify(string passwordHash, string password)
    {
        Verifies++;
        return inner.Verify(passwordHash, password);
    }

    public bool VerifyUnknownAccount(string password)
    {
        UnknownAccountChecks++;
        return inner.VerifyUnknownAccount(password);
    }
}
