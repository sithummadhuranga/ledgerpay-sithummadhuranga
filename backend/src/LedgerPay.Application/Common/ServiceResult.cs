namespace LedgerPay.Application.Common;

// A service answers with a value or with the code of the business rule that stopped it.
// The API turns the code into a Problem Details response. Replayed is true when the answer
// was stored earlier under the same Idempotency-Key. RetryAfterSeconds is set when the caller
// has to wait, for example while an account is locked.
public sealed class ServiceResult<T>
{
    private ServiceResult(T? value, string? errorCode, bool replayed, int? retryAfterSeconds)
    {
        Value = value;
        ErrorCode = errorCode;
        Replayed = replayed;
        RetryAfterSeconds = retryAfterSeconds;
    }

    public T? Value { get; }

    public string? ErrorCode { get; }

    public bool Replayed { get; }

    public int? RetryAfterSeconds { get; }

    public bool Succeeded => ErrorCode is null;

    // Keeps the outcome and changes only the value, so a caller that wants a part of it does not rebuild the failure.
    public ServiceResult<TOut> Map<TOut>(Func<T, TOut> convert) =>
        Succeeded
            ? ServiceResult<TOut>.Ok(convert(Value!), Replayed)
            : ServiceResult<TOut>.Failed(ErrorCode!, Replayed, RetryAfterSeconds);

    public static ServiceResult<T> Ok(T value, bool replayed = false) => new(value, null, replayed, null);

    public static ServiceResult<T> Fail(string errorCode, bool replayed = false) => new(default, errorCode, replayed, null);

    internal static ServiceResult<T> Failed(string errorCode, bool replayed, int? retryAfterSeconds) =>
        new(default, errorCode, replayed, retryAfterSeconds);

    public static ServiceResult<T> FailAndWait(string errorCode, int retryAfterSeconds) =>
        new(default, errorCode, false, retryAfterSeconds);
}
