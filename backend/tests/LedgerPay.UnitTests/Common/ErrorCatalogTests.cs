using System.Reflection;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;

namespace LedgerPay.UnitTests.Common;

public class ErrorCatalogTests
{
    private static readonly (string Code, int Status)[] Statuses =
    [
        (ErrorCodes.ValidationFailed, 400),
        (ErrorCodes.IdempotencyKeyRequired, 400),
        (ErrorCodes.Unauthenticated, 401),
        (ErrorCodes.InvalidCredentials, 401),
        (ErrorCodes.Forbidden, 403),
        (ErrorCodes.AccountLocked, 423),
        (ErrorCodes.InvalidRefreshToken, 401),
        (ErrorCodes.AccountRestricted, 403),
        (ErrorCodes.StaffNotFound, 404),
        (ErrorCodes.StaffNotRestrictable, 422),
        (ErrorCodes.AccountAlreadyInState, 409),
        (ErrorCodes.SessionNotFound, 404),
        (ErrorCodes.WalletNotFound, 404),
        (ErrorCodes.TransactionNotFound, 404),
        (ErrorCodes.RecipientNotFound, 404),
        (ErrorCodes.EmailAlreadyRegistered, 409),
        (ErrorCodes.PhoneAlreadyRegistered, 409),
        (ErrorCodes.DuplicateBankReference, 409),
        (ErrorCodes.IdempotencyKeyReused, 409),
        (ErrorCodes.WalletAlreadyInState, 409),
        (ErrorCodes.SelfTransferNotAllowed, 422),
        (ErrorCodes.AmountBelowMinimum, 422),
        (ErrorCodes.AmountAboveMaximum, 422),
        (ErrorCodes.WalletFrozen, 422),
        (ErrorCodes.InsufficientFunds, 422),
        (ErrorCodes.ReceiverBalanceLimitExceeded, 422),
        (ErrorCodes.BalanceLimitExceeded, 422),
        (ErrorCodes.RateLimited, 429),
        (ErrorCodes.NotFound, 404),
        (ErrorCodes.MethodNotAllowed, 405),
        (ErrorCodes.PayloadTooLarge, 413),
        (ErrorCodes.UnsupportedMediaType, 415),
        (ErrorCodes.InternalError, 500)
    ];

    public static TheoryData<string, int> ExpectedStatuses
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var (code, status) in Statuses)
            {
                data.Add(code, status);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ExpectedStatuses))]
    public void Each_code_maps_to_the_status_in_the_error_table(string code, int status)
    {
        Assert.Equal(status, ErrorCatalog.Describe(code).Status);
    }

    [Fact]
    public void Every_error_code_constant_has_an_entry()
    {
        var codes = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(codes);
        Assert.All(codes, code => Assert.False(string.IsNullOrWhiteSpace(ErrorCatalog.Describe(code).Title)));
    }

    [Fact]
    public void The_table_in_this_test_covers_every_error_code_constant()
    {
        var constants = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Count(field => field.IsLiteral);

        Assert.Equal(constants, Statuses.Length);
    }

    [Fact]
    public void An_unknown_code_is_a_programming_error()
    {
        Assert.Throws<ArgumentException>(() => ErrorCatalog.Describe("NOT_A_CODE"));
    }

    [Fact]
    public void Titles_are_plain_sentences_without_dashes_or_exclamation_marks()
    {
        var codes = Statuses.Select(row => row.Code);

        Assert.All(codes, code =>
        {
            var title = ErrorCatalog.Describe(code).Title;
            Assert.DoesNotContain('\u2014', title);
            Assert.DoesNotContain('!', title);
            Assert.False(title.EndsWith('.'));
        });
    }
}
