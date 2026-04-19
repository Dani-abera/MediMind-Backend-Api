using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MediMind.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MediMindDbContext>
{
    public MediMindDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MEDIMIND_DESIGN_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(ResolveApiContentRoot())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .Build();
                connectionString = configuration.GetConnectionString("DefaultConnection");
            }
            catch (Exception)
            {
                connectionString =
                    "Host=localhost;Port=5432;Database=mediminddb;Username=postgres;Password=postgres";
            }
        }

        var optionsBuilder = new DbContextOptionsBuilder<MediMindDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new MediMindDbContext(optionsBuilder.Options, null, null);
    }

    private static string ResolveApiContentRoot()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "MediMind.API"),
            Path.Combine(Directory.GetCurrentDirectory(), "MediMind.API"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "MediMind.API"),
            Directory.GetCurrentDirectory()
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (File.Exists(Path.Combine(full, "appsettings.json")))
                return full;
        }

        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir?.Parent is not null; dir = dir.Parent)
        {
            var fromSrc = Path.Combine(dir.FullName, "src", "MediMind.API");
            if (File.Exists(Path.Combine(fromSrc, "appsettings.json")))
                return Path.GetFullPath(fromSrc);
        }

        return Directory.GetCurrentDirectory();
    }
}