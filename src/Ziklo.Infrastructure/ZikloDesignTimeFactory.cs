using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ziklo.Infrastructure;

public sealed class ZikloDesignTimeFactory : IDesignTimeDbContextFactory<ZikloDbContext>
{
    public ZikloDbContext CreateDbContext(string[] args)
    {
        // Migration scaffolding does not open this connection. Database updates require
        // an explicit environment connection string so they cannot target a default DB.
        string connection = Environment.GetEnvironmentVariable("ConnectionStrings__Ziklo")
            ?? "Server=127.0.0.1,1;Database=ZikloDesignOnly;User Id=invalid;Password=invalid;Connect Timeout=1;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<ZikloDbContext>()
            .UseSqlServer(connection)
            .Options;
        return new ZikloDbContext(options);
    }
}
