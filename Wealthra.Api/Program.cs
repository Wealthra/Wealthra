using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Microsoft.EntityFrameworkCore;
using Wealthra.Api.Infrastructure;
using Wealthra.Application;
using Wealthra.Infrastructure;
using Wealthra.Infrastructure.Persistence;
using Microsoft.OpenApi; // Note the change to Models namespace

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

// 1. Setup Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// 2. Add Services to DI Container
builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin => true) 
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// OpenAPI
// Native .NET 10 OpenAPI replacement
builder.Services.AddOpenApi(options =>
{
    // 1. DOCUMENT TRANSFORMER: Defines the Security Scheme
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Wealthra API",
            Version = "v1",
            Description = "Wealthra Wealth Management API"
        };

        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token (e.g. eyJhbG...)"
        };

        // Safely initialize and add the scheme to the document components
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = scheme;

        return Task.CompletedTask;
    });

    // 2. OPERATION TRANSFORMER: Applies the lock dynamically based on attributes
    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        
        var hasAuthorize = metadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any();
        var allowAnonymous = metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any();

        if (hasAuthorize && !allowAnonymous)
        {
            operation.Security ??= new List<OpenApiSecurityRequirement>();
            
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = new List<string>()
            });
        }

        return Task.CompletedTask;
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(); 

// --- HEALTH CHECKS ---
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

// 3. Add Application Layers
builder.Services.AddApplicationLayer();
builder.Services.AddInfrastructureLayer(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// 4. Configure Request Pipeline
// Generates the JSON spec at /openapi/v1.json
app.MapOpenApi();
// Visual UI for the spec
app.UseSwaggerUI(options =>
{
        options.SwaggerEndpoint("/openapi/v1.json", "Wealthra API v1");
});

app.UseSerilogRequestLogging();
app.UseExceptionHandler();

// app.UseHttpsRedirection(); // Uncomment if using HTTPS
app.UseCors("AllowFrontend");

app.MapHealthChecks("/health");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 5. Database Migration and Seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    int maxRetries = 5;
    int retryDelaySeconds = 3;
    
    for (int retry = 1; retry <= maxRetries; retry++)
    {
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            if (context.Database.IsRelational())
            {
                await context.Database.MigrateAsync();
            }

            // 2. Seed Identity Data
            await Wealthra.Infrastructure.Persistence.Seeding.IdentitySeeder.SeedDefaultUsersAndRolesAsync(services);

            // 3. Seed Demo Data (categories, incomes, expenses, budgets, goals)
            await Wealthra.Infrastructure.Persistence.Seeding.DataSeeder.SeedDemoDataAsync(services);
            
            break; // Success! Exit the retry loop
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            if (retry == maxRetries)
            {
                logger.LogCritical(ex, "Failed to migrate or seed database after {MaxRetries} attempts. Application will start but may be unstable.", maxRetries);
            }
            else
            {
                logger.LogWarning(ex, "Database connection failed on attempt {Retry}/{MaxRetries}. Retrying in {Delay} seconds...", retry, maxRetries, retryDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
            }
        }
    }
}

app.Run();