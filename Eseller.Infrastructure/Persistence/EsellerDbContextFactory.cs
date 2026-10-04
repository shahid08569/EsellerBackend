using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Eseller.Infrastructure.Persistence;

public class EsellerDbContextFactory : IDesignTimeDbContextFactory<EsellerDbContext>
{
    public EsellerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Eseller.API"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        var optionsBuilder = new DbContextOptionsBuilder<EsellerDbContext>();
        optionsBuilder.UseSqlServer(connectionString, b =>
        {
            b.MigrationsAssembly("Eseller.Infrastructure");
            b.CommandTimeout(180);
        });

        return new EsellerDbContext(optionsBuilder.Options);
    }
}