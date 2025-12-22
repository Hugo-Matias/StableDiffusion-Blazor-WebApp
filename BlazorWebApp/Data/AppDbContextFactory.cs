using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BlazorWebApp.Data
{
    /// <summary>
    /// Design-time factory for EF Core migrations.
    /// This allows dotnet ef migrations to work without needing the full application service provider.
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlite("Data Source=BlazorWebApp.db");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
