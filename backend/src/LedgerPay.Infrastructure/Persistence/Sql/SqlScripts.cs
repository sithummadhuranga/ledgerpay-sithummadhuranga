using System.Reflection;

namespace LedgerPay.Infrastructure.Persistence.Sql;

internal static class SqlScripts
{
    public const string BlockLedgerEntryChanges = "BlockLedgerEntryChanges.sql";
    public const string BlockAuditLogChanges = "BlockAuditLogChanges.sql";
    public const string WalletStatementView = "WalletStatementView.sql";
    public const string ApplyApiPermissions = "ApplyApiPermissions.sql";

    public static string Read(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("." + fileName, StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // CREATE TRIGGER and CREATE VIEW must be the first statement in a batch, but the idempotent
    // migration script wraps each migration in an IF block. EXEC runs them in a batch of their own.
    public static string AsExec(string fileName)
    {
        var escaped = Read(fileName).Replace("'", "''");
        return $"EXEC(N'{escaped}')";
    }
}
