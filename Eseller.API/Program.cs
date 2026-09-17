//namespaces
using Eseller.Application;
using Eseller.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ✅ Serilog — Configuration se load karo
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

// Application services (MediatR, FluentValidation, Mapster)
builder.Services.AddApplicationServices();

// Infrastructure services (DbContext, future: repositories, JWT, etc.)
builder.Services.AddInfrastructureServices(builder.Configuration);

// API services.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

//Serilog request logging - middleware
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();// OpenAPI JSON endpoint

    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Serilog flush on shutdown
try
{
    Log.Information("Starting Eseller API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Eseller API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
