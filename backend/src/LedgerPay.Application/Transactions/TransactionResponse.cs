using LedgerPay.Domain.Enums;

namespace LedgerPay.Application.Transactions;

// A customer gets Direction, a masked CounterpartyName and their own side of the fee. The back office gets the
// wallet numbers and the bank reference instead. FailureCode is set only on a failed attempt.
public sealed record TransactionResponse(
    string Reference,
    TransactionType Type,
    TransactionStatus Status,
    TransactionDirection? Direction,
    decimal Amount,
    decimal Fee,
    string? CounterpartyName,
    string? Note,
    string? FailureCode,
    DateTime CreatedAt,
    string? SenderWalletNumber,
    string? ReceiverWalletNumber,
    string? BankReference);
