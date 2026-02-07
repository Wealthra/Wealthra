using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Wealthra.Infrastructure.Services; // Ensure this matches your namespace
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Use the connection string you verified earlier
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=WealthraDb;Username=postgres;Password=isN4il1o4O9");

        // Provide a dummy or real instance of DateTimeService
        var dateTimeService = new DateTimeService();

        return new ApplicationDbContext(optionsBuilder.Options, dateTimeService);
    }
}