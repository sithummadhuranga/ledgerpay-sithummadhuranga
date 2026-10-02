namespace LedgerPay.Infrastructure.Persistence.Configurations;

internal static class CheckSql
{
    // Builds [Column] IN ('A', 'B') from the enum, so the CHECK constraint cannot drift from the code.
    public static string In<TEnum>(string column) where TEnum : struct, Enum
    {
        var values = string.Join(", ", Enum.GetNames<TEnum>().Select(name => $"'{name}'"));
        return $"[{column}] IN ({values})";
    }
}
