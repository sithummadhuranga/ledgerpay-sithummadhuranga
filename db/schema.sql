-- LedgerPay database schema as plain SQL, for anyone who does not have the .NET SDK.
--
-- Run it against an empty database. sqlcmd needs the -I flag, because the filtered indexes require
-- QUOTED_IDENTIFIER ON and sqlcmd leaves it off by default:
--   sqlcmd -S <server> -d <database> -I -b -i db/schema.sql
-- The script is idempotent, so running it twice is safe.
--
-- Do not edit the part below the marker. It is generated from the EF Core migrations:
--   dotnet ef migrations script --idempotent --project src/LedgerPay.Infrastructure
-- CI regenerates it on every build and fails when this file is out of date.
--
-- The triggers and the statement view appear as EXEC(N'...') strings. They are written by hand in
-- Persistence/Sql/*.sql because EF Core cannot model them. CREATE TRIGGER and CREATE VIEW must start a batch
-- of their own, and the idempotent script wraps every migration in an IF block, so each one runs through EXEC.
--
-- Not in this file: the api login permissions and the seed data, which the DbTool applies
-- (setup, or permissions and seed on their own), and the local Docker logins, which come from db/init.
--
-- GENERATED SCRIPT STARTS BELOW
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] varchar(20) NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE TABLE [SystemSettings] (
        [Id] uniqueidentifier NOT NULL,
        [Key] varchar(100) NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SystemSettings_Value_NonNegative] CHECK ([Value] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [Email] varchar(254) NOT NULL,
        [Phone] varchar(12) NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [PasswordHash] varchar(256) NOT NULL,
        [FailedLoginCount] int NOT NULL,
        [LockoutEnd] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Users_Email_Lowercase] CHECK ([Email] COLLATE Latin1_General_BIN2 = LOWER([Email])),
        CONSTRAINT [CK_Users_FailedLoginCount_NonNegative] CHECK ([FailedLoginCount] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [Action] varchar(50) NOT NULL,
        [EntityType] varchar(50) NOT NULL,
        [EntityReference] varchar(100) NULL,
        [IpAddress] varchar(45) NULL,
        [CorrelationId] varchar(64) NULL,
        [Details] nvarchar(1000) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE TABLE [Wallets] (
        [Id] uniqueidentifier NOT NULL,
        [WalletNumber] varchar(12) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Balance] decimal(18,2) NOT NULL,
        [Status] varchar(10) NOT NULL,
        [StatusReason] nvarchar(250) NULL,
        [StatusChangedByUserId] uniqueidentifier NULL,
        [StatusChangedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Wallets] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Wallets_Balance_NonNegative] CHECK ([Balance] >= 0),
        CONSTRAINT [CK_Wallets_Status_Valid] CHECK ([Status] IN ('Active', 'Frozen')),
        CONSTRAINT [CK_Wallets_WalletNumber_TwelveDigits] CHECK (LEN([WalletNumber]) = 12 AND [WalletNumber] NOT LIKE '%[^0-9]%'),
        CONSTRAINT [FK_Wallets_Users_StatusChangedByUserId] FOREIGN KEY ([StatusChangedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Wallets_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE TABLE [LedgerAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [Code] varchar(50) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Type] varchar(10) NOT NULL,
        [WalletId] uniqueidentifier NULL,
        CONSTRAINT [PK_LedgerAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_LedgerAccounts_Type_Valid] CHECK ([Type] IN ('Asset', 'Liability', 'Revenue')),
        CONSTRAINT [FK_LedgerAccounts_Wallets_WalletId] FOREIGN KEY ([WalletId]) REFERENCES [Wallets] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE TABLE [Transactions] (
        [Id] uniqueidentifier NOT NULL,
        [Reference] varchar(24) NOT NULL,
        [Type] varchar(10) NOT NULL,
        [Status] varchar(10) NOT NULL,
        [FailureCode] varchar(50) NULL,
        [SenderWalletId] uniqueidentifier NULL,
        [ReceiverWalletId] uniqueidentifier NULL,
        [RequestedReceiver] nvarchar(50) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Fee] decimal(18,2) NOT NULL,
        [Note] nvarchar(140) NULL,
        [BankReference] varchar(40) NULL,
        [InitiatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Transactions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Transactions_Amount_Positive] CHECK ([Amount] > 0),
        CONSTRAINT [CK_Transactions_FailureCode_OnlyWhenFailed] CHECK (([Status] = 'Failed' AND [FailureCode] IS NOT NULL) OR ([Status] = 'Completed' AND [FailureCode] IS NULL)),
        CONSTRAINT [CK_Transactions_Fee_NonNegative] CHECK ([Fee] >= 0),
        CONSTRAINT [CK_Transactions_Status_Valid] CHECK ([Status] IN ('Completed', 'Failed')),
        CONSTRAINT [CK_Transactions_Type_Valid] CHECK ([Type] IN ('TopUp', 'Transfer')),
        CONSTRAINT [FK_Transactions_Users_InitiatedByUserId] FOREIGN KEY ([InitiatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Transactions_Wallets_ReceiverWalletId] FOREIGN KEY ([ReceiverWalletId]) REFERENCES [Wallets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Transactions_Wallets_SenderWalletId] FOREIGN KEY ([SenderWalletId]) REFERENCES [Wallets] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE TABLE [IdempotencyKeys] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Key] varchar(100) NOT NULL,
        [Endpoint] varchar(100) NOT NULL,
        [RequestHash] varchar(64) NOT NULL,
        [ResponseStatusCode] int NULL,
        [ResponseBody] nvarchar(max) NULL,
        [TransactionId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_IdempotencyKeys] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IdempotencyKeys_Transactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [Transactions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_IdempotencyKeys_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE TABLE [LedgerEntries] (
        [Id] uniqueidentifier NOT NULL,
        [Sequence] bigint NOT NULL IDENTITY,
        [TransactionId] uniqueidentifier NOT NULL,
        [LedgerAccountId] uniqueidentifier NOT NULL,
        [Debit] decimal(18,2) NOT NULL,
        [Credit] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_LedgerEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_LedgerEntries_Amounts_NonNegative] CHECK ([Debit] >= 0 AND [Credit] >= 0),
        CONSTRAINT [CK_LedgerEntries_ExactlyOneSide] CHECK (([Debit] > 0 AND [Credit] = 0) OR ([Debit] = 0 AND [Credit] > 0)),
        CONSTRAINT [FK_LedgerEntries_LedgerAccounts_LedgerAccountId] FOREIGN KEY ([LedgerAccountId]) REFERENCES [LedgerAccounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_LedgerEntries_Transactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [Transactions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ActorUserId] ON [AuditLogs] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedAt] ON [AuditLogs] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_IdempotencyKeys_TransactionId] ON [IdempotencyKeys] ([TransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_IdempotencyKeys_UserId_Key_Endpoint] ON [IdempotencyKeys] ([UserId], [Key], [Endpoint]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LedgerAccounts_Code] ON [LedgerAccounts] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_LedgerAccounts_WalletId] ON [LedgerAccounts] ([WalletId]) WHERE [WalletId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LedgerEntries_LedgerAccountId_Sequence] ON [LedgerEntries] ([LedgerAccountId], [Sequence]) INCLUDE ([CreatedAt], [Debit], [Credit], [TransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_LedgerEntries_TransactionId] ON [LedgerEntries] ([TransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SystemSettings_Key] ON [SystemSettings] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Transactions_BankReference] ON [Transactions] ([BankReference]) WHERE [BankReference] IS NOT NULL AND [Status] = ''Completed''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Transactions_InitiatedByUserId] ON [Transactions] ([InitiatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Transactions_ReceiverWalletId_CreatedAt] ON [Transactions] ([ReceiverWalletId], [CreatedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Transactions_Reference] ON [Transactions] ([Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Transactions_SenderWalletId_CreatedAt] ON [Transactions] ([SenderWalletId], [CreatedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Phone] ON [Users] ([Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Wallets_StatusChangedByUserId] ON [Wallets] ([StatusChangedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Wallets_UserId] ON [Wallets] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Wallets_WalletNumber] ON [Wallets] ([WalletNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    EXEC(N'CREATE OR ALTER TRIGGER [dbo].[trg_LedgerEntries_BlockChanges]
    ON [dbo].[LedgerEntries]
    INSTEAD OF UPDATE, DELETE
    AS
    BEGIN
        SET NOCOUNT ON;
        THROW 51000, ''LedgerEntries is append-only. UPDATE and DELETE are not allowed.'', 1;
    END
    ')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    EXEC(N'CREATE OR ALTER TRIGGER [dbo].[trg_AuditLogs_BlockChanges]
    ON [dbo].[AuditLogs]
    INSTEAD OF UPDATE, DELETE
    AS
    BEGIN
        SET NOCOUNT ON;
        THROW 51001, ''AuditLogs is append-only. UPDATE and DELETE are not allowed.'', 1;
    END
    ')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    EXEC(N'CREATE OR ALTER VIEW [dbo].[vw_WalletStatement]
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
    ')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002152252_InitialSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002152252_InitialSchema', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004141755_AddRefreshTokens'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [FamilyId] uniqueidentifier NOT NULL,
        [TokenHash] char(64) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [SessionStartedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        [ReplacedById] uniqueidentifier NULL,
        [IpAddress] varchar(45) NULL,
        [UserAgent] nvarchar(200) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004141755_AddRefreshTokens'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_FamilyId_UserId] ON [RefreshTokens] ([FamilyId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004141755_AddRefreshTokens'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004141755_AddRefreshTokens'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004141755_AddRefreshTokens'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004141755_AddRefreshTokens', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004150929_AddBackOffice'
)
BEGIN
    ALTER TABLE [Users] ADD [RestrictedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004150929_AddBackOffice'
)
BEGIN
    ALTER TABLE [Users] ADD [RestrictedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004150929_AddBackOffice'
)
BEGIN
    ALTER TABLE [Users] ADD [RestrictedReason] nvarchar(250) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004150929_AddBackOffice'
)
BEGIN
    CREATE INDEX [IX_Users_RestrictedByUserId] ON [Users] ([RestrictedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004150929_AddBackOffice'
)
BEGIN
    CREATE INDEX [IX_Transactions_CreatedAt] ON [Transactions] ([CreatedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004150929_AddBackOffice'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Action_CreatedAt] ON [AuditLogs] ([Action], [CreatedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004150929_AddBackOffice'
)
BEGIN
    ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Users_RestrictedByUserId] FOREIGN KEY ([RestrictedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004150929_AddBackOffice'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004150929_AddBackOffice', N'10.0.12');
END;

COMMIT;
GO

