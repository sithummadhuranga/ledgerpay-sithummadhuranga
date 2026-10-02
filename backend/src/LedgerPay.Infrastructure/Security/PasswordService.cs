using LedgerPay.Application.Common.Abstractions;
using LedgerPay.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace LedgerPay.Infrastructure.Security;

// The ASP.NET Core Identity hasher (PBKDF2 with a random salt), used without the rest of Identity.
public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> hasher = new();

    // The hasher takes a user object but never reads it, so null is fine.
    public string Hash(string password) => hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password) =>
        hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
}
