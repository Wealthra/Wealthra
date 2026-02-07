using Serilog;
using Wealthra.Application;    // You will create this extension method later
using Wealthra.Infrastructure; // You will create this extension method later

var builder = WebApplication.CreateBuilder(args);

// 1. Setup Serilog (Logging)
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// 2. Add Services to DI Container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. Add Layers (We will write these extension methods in the next steps)
// builder.Services.AddApplicationLayer();
// builder.Services.AddInfrastructureLayer(builder.Configuration);

var app = builder.Build();

// 4. Configure Request Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging(); // Logs every HTTP request
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();