namespace LedgerPay.Application.Transfers;

public sealed record QuoteResponse(decimal Amount, decimal Fee, decimal Total);
