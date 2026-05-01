using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Api.IntegrationTests;

public class WealthraApiFactory : WebApplicationFactory<Program>
{
    public const string InternalAiUsageKey = "integration-test-ai-ingest-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalApi:AiUsageIngestKey"] = InternalAiUsageKey,
            });
        });

        builder.ConfigureServices(services =>
        {
            // Drop the API's Npgsql DbContext registration entirely so only InMemory is registered.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IApplicationDbContext>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("WealthraApiIntegrationTests"));

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        });
    }
}
