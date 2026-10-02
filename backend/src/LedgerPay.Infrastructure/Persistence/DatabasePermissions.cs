using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Infrastructure.Persistence;

public static class DatabasePermissions
{
    // The login name goes in as a parameter. The script quotes it with QUOTENAME before using it in dynamic SQL.
    public static async Task ApplyAsync(AppDbContext db, string apiUser, CancellationToken cancellationToken)
    {
        var script = "DECLARE @ApiUser sysname = @ApiUserName;\n" + SqlScripts.Read(SqlScripts.ApplyApiPermissions);

        await db.Database.ExecuteSqlRawAsync(
            script,
            [new SqlParameter("ApiUserName", apiUser)],
            cancellationToken);
    }
}
