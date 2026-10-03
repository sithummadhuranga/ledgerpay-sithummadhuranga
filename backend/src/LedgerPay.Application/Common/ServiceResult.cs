namespace LedgerPay.Application.Common;

// A service answers with a value or with the code of the business rule that stopped it.
// The API turns the code into a Problem Details response. Replayed is true when the answer
// was stored earlier under the same Idempotency-Key.
public sealed class ServiceResult<T>
{
    private ServiceResult(T? value, string? errorCode, bool replayed)
    {
        Value = value;
        ErrorCode = errorCode;
        Replayed = replayed;
    }

    public T? Value { get; }

    public string? ErrorCode { get; }

    public bool Replayed { get; }

    public bool Succeeded => ErrorCode is null;

    public static ServiceResult<T> Ok(T value, bool replayed = false) => new(value, null, replayed);

    public static ServiceResult<T> Fail(string errorCode, bool replayed = false) => new(default, errorCode, replayed);
}
