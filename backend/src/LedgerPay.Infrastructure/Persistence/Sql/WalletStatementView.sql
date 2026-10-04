CREATE OR ALTER VIEW [dbo].[vw_WalletStatement]
AS
SELECT
    account.WalletId,
    entry.Id AS EntryId,
    entry.TransactionId,
    entry.Sequence,
    entry.CreatedAt,
    entry.Debit,
    entry.Credit,
    -- Wallet accounts are liabilities, so the balance is credits minus debits.
    -- Sequence is the append order. A wallet is locked before it is written, so it matches posting order
    -- even when the clock was read earlier.
    SUM(entry.Credit - entry.Debit) OVER (
        PARTITION BY account.WalletId
        ORDER BY entry.Sequence
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS BalanceAfter
FROM [dbo].[LedgerEntries] AS entry
INNER JOIN [dbo].[LedgerAccounts] AS account ON account.Id = entry.LedgerAccountId
WHERE account.WalletId IS NOT NULL;
