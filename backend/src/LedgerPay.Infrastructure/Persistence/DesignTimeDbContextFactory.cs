using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LedgerPay.Infrastructure.Persistence;

// Used only by dotnet ef. Adding a migration or scripting them never opens a connection,
// so a placeholder connection string is enough and no secret is needed.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=LedgerPay;Trusted_Connection=True;")
            .Options;

        return new AppDbContext(options);
    }
}
