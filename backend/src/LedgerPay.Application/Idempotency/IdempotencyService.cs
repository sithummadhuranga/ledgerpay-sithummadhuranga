using System.Text.Json;
using System.Text.RegularExpressions;
using LedgerPay.Application.Abstractions;
using LedgerPay.Application.Common;
using LedgerPay.Domain.Constants;
using LedgerPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Application.Idempotency;

public sealed class IdempotencyService(IAppDbContext db, TimeProvider clock)
{
    public const int MaximumKeyLength = 100;

    // Letters, digits, _ and -. A UUID fits. The column is a case-insensitive varchar, so keys that
    // differ only in letter case are the same key, and anything outside ASCII is refused instead of being squashed.
    private static readonly Regex KeyPattern = new(@"\A[A-Za-z0-9_-]+\z", RegexOptions.Compiled);

    private const int Created = 201;

    // The same options ASP.NET Core uses, so a stored answer reads back the way it was first sent.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Returns the error code for a key that cannot be used, or null. The money services call this before they
    // look anything up, so a missing key is reported first.
    public static string? ValidateKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return ErrorCodes.IdempotencyKeyRequired;
        }

        return key.Length > MaximumKeyLength || !KeyPattern.IsMatch(key) ? ErrorCodes.ValidationFailed : null;
    }

    // Runs a money operation once per key. The key row goes in first, inside the money transaction, so a second
    // request with the same key waits on the unique index, then fails and reads the first one's stored answer.
    public async Task<ServiceResult<T>> RunAsync<T>(
        Guid userId,
        string? key,
        string endpoint,
        string requestHash,
        Func<IdempotencyKey, CancellationToken, Task<ServiceResult<T>>> work,
        CancellationToken cancellationToken)
    {
        var keyError = ValidateKey(key);
        if (keyError is not null)
        {
            return ServiceResult<T>.Fail(keyError);
        }

        try
        {
            return await db.ExecuteInTransactionAsync(async token =>
            {
                var record = new IdempotencyKey
                {
                    UserId = userId,
                    Key = key!,
                    Endpoint = endpoint,
                    RequestHash = requestHash,
                    CreatedAt = clock.GetUtcNow().UtcDateTime
                };
                db.IdempotencyKeys.Add(record);
                await db.SaveChangesAsync(token);

                var result = await work(record, token);

                Store(record, result);
                await db.SaveChangesAsync(token);
                return result;
            }, cancellationToken);
        }
        catch (DbUpdateException error) when (db.IsDuplicateKey(error))
        {
            // The transaction has rolled back. If a committed row holds this key, answer from it.
            var replay = await ReplayAsync<T>(userId, key!, endpoint, requestHash, cancellationToken);
            if (replay is null)
            {
                throw;
            }

            return replay;
        }
    }

    // A success is kept as the JSON that was sent. A rejection is kept as its error code, because the API
    // builds the Problem Details again from the code, with the trace id of the new request.
    private static void Store<T>(IdempotencyKey record, ServiceResult<T> result)
    {
        record.ResponseStatusCode = result.Succeeded ? Created : ErrorCatalog.Describe(result.ErrorCode!).Status;
        record.ResponseBody = result.Succeeded ? JsonSerializer.Serialize(result.Value, Json) : result.ErrorCode;
    }

    private async Task<ServiceResult<T>?> ReplayAsync<T>(
        Guid userId, string key, string endpoint, string requestHash, CancellationToken cancellationToken)
    {
        var stored = await db.IdempotencyKeys.AsNoTracking().SingleOrDefaultAsync(
            row => row.UserId == userId && row.Key == key && row.Endpoint == endpoint, cancellationToken);

        if (stored is null)
        {
            return null;
        }

        if (stored.RequestHash != requestHash)
        {
            return ServiceResult<T>.Fail(ErrorCodes.IdempotencyKeyReused);
        }

        // The key row and its answer are written in one transaction, so a committed row always has both.
        if (stored.ResponseStatusCode is null || stored.ResponseBody is null)
        {
            throw new InvalidOperationException($"Idempotency key {stored.Id} was committed without a stored answer.");
        }

        if (stored.ResponseStatusCode >= 400)
        {
            return ServiceResult<T>.Fail(stored.ResponseBody, replayed: true);
        }

        var value = JsonSerializer.Deserialize<T>(stored.ResponseBody, Json)
            ?? throw new InvalidOperationException($"Stored answer for idempotency key {stored.Id} could not be read.");
        return ServiceResult<T>.Ok(value, replayed: true);
    }
}
