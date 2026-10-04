using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LedgerPay.Infrastructure.Persistence.Sql;

public static class DatabasePermissions
{
    // The names go in as parameters. The script quotes the user name with QUOTENAME before using it in dynamic SQL.
    // With no password the user belongs to a server login. With a password it is made inside the database, which is
    // what Azure SQL needs because it has no server logins for us to use.
    public static async Task ApplyAsync(
        AppDbContext db, string apiUser, CancellationToken cancellationToken, string? apiPassword = null)
    {
        var script = "DECLARE @ApiUser sysname = @ApiUserName;\nDECLARE @ApiPassword nvarchar(128) = @ApiPasswordValue;\n"
            + SqlScripts.Read(SqlScripts.ApplyApiPermissions);

        await db.Database.ExecuteSqlRawAsync(
            script,
            [
                new SqlParameter("ApiUserName", apiUser),
                new SqlParameter("ApiPasswordValue", (object?)apiPassword ?? DBNull.Value)
            ],
            cancellationToken);
    }
}
