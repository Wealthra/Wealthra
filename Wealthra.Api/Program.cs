using Serilog;
using Wealthra.Api.Infrastructure;
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.UseSerilogRequestLogging(); // Logs every HTTP request
app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();