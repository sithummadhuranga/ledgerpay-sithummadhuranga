CREATE OR ALTER TRIGGER [dbo].[trg_LedgerEntries_BlockChanges]
ON [dbo].[LedgerEntries]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, 'LedgerEntries is append-only. UPDATE and DELETE are not allowed.', 1;
END
