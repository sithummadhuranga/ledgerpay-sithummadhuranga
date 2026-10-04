using LedgerPay.Application.Abstractions;
using LedgerPay.Application.Admin;
using LedgerPay.Application.Auth;
using LedgerPay.Application.BackOffice;
using LedgerPay.Application.Idempotency;
using LedgerPay.Application.Settings;
using LedgerPay.Application.TopUps;
using LedgerPay.Application.Transfers;
using LedgerPay.Infrastructure.Persistence;
using LedgerPay.Infrastructure.Security;

namespace LedgerPay.IntegrationTests.Support;

// Builds the real services over a real context, the way the container would.
internal static class TestServices
{
    public static string NewKey() => Guid.NewGuid().ToString();

    public static string NewBankReference() => "BANK" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    public static AuthService Auth(AppDbContext db, TimeProvider? clock = null, JwtOptions? jwt = null, IPasswordService? passwords = null)
    {
        clock ??= TimeProvider.System;
        var tokens = new JwtTokenService(jwt ?? TestJwt.Options());
        return new AuthService(db, passwords ?? new PasswordService(), tokens, Sessions(db, clock, jwt), clock);
    }

    public static SessionService Sessions(AppDbContext db, TimeProvider? clock = null, JwtOptions? jwt = null) =>
        new(db, new JwtTokenService(jwt ?? TestJwt.Options()), new RefreshTokenService(), clock ?? TimeProvider.System);

    public static BackOfficeQueries BackOffice(AppDbContext db, TimeProvider? clock = null) => new(db, clock ?? TimeProvider.System);

    public static StaffService Staff(AppDbContext db, TimeProvider? clock = null) => new(db, clock ?? TimeProvider.System);

    public static TransferService Transfers(AppDbContext db, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        return new TransferService(db, new LedgerSettingsProvider(db), new IdempotencyService(db, clock), clock);
    }

    public static TopUpService TopUps(AppDbContext db, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        return new TopUpService(db, new LedgerSettingsProvider(db), new IdempotencyService(db, clock), clock);
    }

    public static WalletStatusService WalletStatus(AppDbContext db, TimeProvider? clock = null) =>
        new(db, clock ?? TimeProvider.System);
}
