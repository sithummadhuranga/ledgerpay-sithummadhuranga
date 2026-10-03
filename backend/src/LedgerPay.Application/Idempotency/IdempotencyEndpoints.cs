namespace LedgerPay.Application.Idempotency;

// A key is scoped to the user and to the endpoint, so the same key on another endpoint is another request.
public static class IdempotencyEndpoints
{
    public const string Transfers = "POST /transfers";
}
