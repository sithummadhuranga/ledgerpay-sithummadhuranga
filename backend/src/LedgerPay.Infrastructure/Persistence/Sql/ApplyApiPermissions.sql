-- @ApiUser is declared by the caller and holds the login the API connects with. Run as the schema owner, never as the API login.
DECLARE @quoted nvarchar(260) = QUOTENAME(@ApiUser);

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @ApiUser)
    EXEC(N'CREATE USER ' + @quoted + N' FOR LOGIN ' + @quoted);

-- No DELETE is granted anywhere. The revokes make a re-run clean up any wider grant from an earlier version.
EXEC(N'REVOKE INSERT, UPDATE ON SCHEMA::dbo FROM ' + @quoted);
EXEC(N'GRANT SELECT ON SCHEMA::dbo TO ' + @quoted);

-- INSERT only into the tables the app writes. Roles, SystemSettings and the migration history stay read-only.
EXEC(N'GRANT INSERT ON [dbo].[Users] TO ' + @quoted);
EXEC(N'GRANT INSERT ON [dbo].[UserRoles] TO ' + @quoted);
EXEC(N'GRANT INSERT ON [dbo].[Wallets] TO ' + @quoted);
EXEC(N'GRANT INSERT ON [dbo].[LedgerAccounts] TO ' + @quoted);
EXEC(N'GRANT INSERT ON [dbo].[Transactions] TO ' + @quoted);
EXEC(N'GRANT INSERT ON [dbo].[LedgerEntries] TO ' + @quoted);
EXEC(N'GRANT INSERT ON [dbo].[IdempotencyKeys] TO ' + @quoted);
EXEC(N'GRANT INSERT ON [dbo].[AuditLogs] TO ' + @quoted);

-- UPDATE only where the app changes existing rows: failed login counts, wallet balances and status, idempotency responses.
EXEC(N'GRANT UPDATE ON [dbo].[Users] TO ' + @quoted);
EXEC(N'GRANT UPDATE ON [dbo].[Wallets] TO ' + @quoted);
EXEC(N'GRANT UPDATE ON [dbo].[IdempotencyKeys] TO ' + @quoted);

EXEC(N'DENY UPDATE, DELETE ON [dbo].[LedgerEntries] TO ' + @quoted);
EXEC(N'DENY UPDATE, DELETE ON [dbo].[AuditLogs] TO ' + @quoted);
