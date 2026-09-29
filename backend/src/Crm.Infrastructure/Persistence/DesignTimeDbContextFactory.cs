using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Crm.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef</c> to create migrations; set <c>CRM_MIGRATIONS_CONNECTION</c> to target another server.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CRM_MIGRATIONS_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=CrmDev;Trusted_Connection=True;TrustServerCertificate=True";
        return new CrmDbContext(new DbContextOptionsBuilder<CrmDbContext>().UseSqlServer(connection).Options);
    }
}
