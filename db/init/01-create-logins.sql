-- Local container only. Creates the database and the two logins the app uses instead of sa.
-- sqlcmd fills $(...) from the environment of the db-init service.
IF DB_ID(N'LedgerPay') IS NULL
    CREATE DATABASE [LedgerPay];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'ledgerpay_migrator')
    CREATE LOGIN [ledgerpay_migrator] WITH PASSWORD = N'$(DB_MIGRATOR_PASSWORD)';
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'ledgerpay_api')
    CREATE LOGIN [ledgerpay_api] WITH PASSWORD = N'$(DB_API_PASSWORD)';
GO

USE [LedgerPay];
GO

-- The migrator owns the schema. The api user is created later by the DbTool, with limited rights.
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'ledgerpay_migrator')
    CREATE USER [ledgerpay_migrator] FOR LOGIN [ledgerpay_migrator];
ALTER ROLE db_owner ADD MEMBER [ledgerpay_migrator];
GO
