using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MediMind.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MediMindDbContext>
{
    public MediMindDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MediMindDbContext>();
        optionsBuilder.UseSqlite("Data Source=medimind.db");

        // Pass null for ICurrentUser and IMediator for design-time
        return new MediMindDbContext(optionsBuilder.Options, null, null);
    }
}

