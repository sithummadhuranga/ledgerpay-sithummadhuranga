namespace LedgerPay.Application.Common;

// A service answers with a value or with the code of the business rule that stopped it.
// The API turns the code into a Problem Details response.
public sealed class ServiceResult<T>
{
    private ServiceResult(T? value, string? errorCode)
    {
        Value = value;
        ErrorCode = errorCode;
    }

    public T? Value { get; }

    public string? ErrorCode { get; }

    public bool Succeeded => ErrorCode is null;

    public static ServiceResult<T> Ok(T value) => new(value, null);

    public static ServiceResult<T> Fail(string errorCode) => new(default, errorCode);
}
