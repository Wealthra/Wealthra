using Serilog;
using Wealthra.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application;
using Wealthra.Infrastructure;
using Wealthra.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Setup Serilog (Logging)
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// 2. Add Services to DI Container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(); 

// --- HEALTH CHECKS SERVICE ---
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

// 3. Add Layers
builder.Services.AddApplicationLayer();
builder.Services.AddInfrastructureLayer(builder.Configuration);

var app = builder.Build();

// 4. Configure Request Pipeline
app.UseSwagger();
app.UseSwaggerUI();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();

//app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


// Apply pending migrations on startup and seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // 1. Migrate Database
        var context = services.GetRequiredService<Wealthra.Infrastructure.Persistence.ApplicationDbContext>();
        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }

        // 2. Seed Identity Data
        await Wealthra.Infrastructure.Persistence.Seeding.IdentitySeeder.SeedDefaultUsersAndRolesAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

app.Run();