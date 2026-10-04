using LedgerPay.Application.Abstractions;
using LedgerPay.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace LedgerPay.Infrastructure.Security;

// The ASP.NET Core Identity hasher (PBKDF2 with a random salt), used without the rest of Identity.
public sealed class PasswordService : IPasswordService
{
    // A hash of a random value that is thrown away, so nobody knows a password for it. It is made once, with the same
    // hasher and settings as every real hash, so checking it costs the same.
    private static readonly string UnknownAccountHash = new PasswordHasher<User>().HashPassword(null!, Guid.NewGuid().ToString("N"));

    private readonly PasswordHasher<User> hasher = new();

    // The hasher takes a user object but never reads it, so null is fine.
    public string Hash(string password) => hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password) =>
        hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;

    public bool VerifyUnknownAccount(string password)
    {
        Verify(UnknownAccountHash, password);
        return false;
    }
}
