using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MediMind.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MediMindDbContext>
{
    public MediMindDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        var optionsBuilder = new DbContextOptionsBuilder<MediMindDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new MediMindDbContext(optionsBuilder.Options, null, null);
    }
}