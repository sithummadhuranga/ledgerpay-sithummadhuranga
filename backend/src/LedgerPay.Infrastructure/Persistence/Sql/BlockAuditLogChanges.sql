CREATE OR ALTER TRIGGER [dbo].[trg_AuditLogs_BlockChanges]
ON [dbo].[AuditLogs]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51001, 'AuditLogs is append-only. UPDATE and DELETE are not allowed.', 1;
END
