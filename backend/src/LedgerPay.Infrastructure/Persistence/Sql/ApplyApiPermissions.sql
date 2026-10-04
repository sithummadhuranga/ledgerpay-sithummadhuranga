-- @ApiUser is declared by the caller and holds the login the API connects with. Run as the schema owner, never as the API login.
DECLARE @quoted nvarchar(260) = QUOTENAME(@ApiUser);

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @ApiUser)
    EXEC(N'CREATE USER ' + @quoted + N' FOR LOGIN ' + @quoted);

-- No DELETE is granted anywhere. The revoke removes a schema-wide INSERT or UPDATE grant left by an earlier version.
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
EXEC(N'GRANT INSERT ON [dbo].[RefreshTokens] TO ' + @quoted);

-- UPDATE only where the app changes existing rows: failed login counts, wallet balances and status, idempotency responses,
-- and, for refresh tokens, only the two columns that say a token was revoked and which token replaced it.
-- Only the columns the app changes: the failed sign-in count, the lock, and the restriction of an account. A password or an email is never changed here.
-- An earlier version granted UPDATE on the whole table. That is removed only if it is there, so running this again
-- never leaves a moment without the right.
IF EXISTS (
    SELECT 1 FROM sys.database_permissions
    WHERE class = 1 AND major_id = OBJECT_ID(N'dbo.Users') AND minor_id = 0
      AND permission_name = 'UPDATE' AND state IN ('G', 'W') AND grantee_principal_id = USER_ID(@ApiUser))
    EXEC(N'REVOKE UPDATE ON [dbo].[Users] FROM ' + @quoted);
EXEC(N'GRANT UPDATE ON [dbo].[Users] ([FailedLoginCount], [LockoutEnd], [RestrictedAt], [RestrictedReason], [RestrictedByUserId]) TO ' + @quoted);
EXEC(N'GRANT UPDATE ON [dbo].[Wallets] TO ' + @quoted);
EXEC(N'GRANT UPDATE ON [dbo].[IdempotencyKeys] TO ' + @quoted);
EXEC(N'GRANT UPDATE ON [dbo].[RefreshTokens] ([RevokedAt], [ReplacedById]) TO ' + @quoted);

EXEC(N'DENY UPDATE, DELETE ON [dbo].[LedgerEntries] TO ' + @quoted);
EXEC(N'DENY UPDATE, DELETE ON [dbo].[AuditLogs] TO ' + @quoted);
