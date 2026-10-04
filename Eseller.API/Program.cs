using Eseller.API.Middleware;
using Eseller.Application;
using Eseller.Application.Abstractions;
using Eseller.Infrastructure;
using Eseller.Infrastructure.BackgroundJobs;
using Eseller.Infrastructure.Persistence;
using Eseller.Infrastructure.SignalR;
using Eseller.Infrastructure.SignalR.Hubs;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ────────────────────────────────────────────────
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

// ─── Application & Infrastructure ───────────────────────────
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// ─── API ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// ─── CORS ───────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "http://localhost:4201",
                "http://localhost:4202",
                "http://localhost:4203",
                "http://localhost:54007",
                "https://eseller.com",
                "https://seller.eseller.com",
                "https://admin.eseller.com",
                "https://affiliate.eseller.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ─── Rate Limiting ──────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var partitionKey = context.User.Identity?.Name
                           ?? context.Connection.RemoteIpAddress?.ToString()
                           ?? "anonymous";

        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Auth endpoints: strict limit (5/min)
        if (path.Contains("/auth/login") || path.Contains("/auth/register"))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"auth-{partitionKey}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
        }

        // Default: 100/min
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ─── Health Checks ──────────────────────────────────────────
builder.Services.AddHealthChecks();

// ─── SignalR ────────────────────────────────────────────────
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<ISignalRNotificationService, SignalRNotificationService>();
builder.Services.AddHostedService<FlashSaleExpiryJob>();

// ─── Exception Handlers ─────────────────────────────────────
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// ─── Middleware Pipeline ────────────────────────────────────
// Quiet request logging in Development (errors still go through exception handler)
if (!app.Environment.IsDevelopment())
{
    app.UseSerilogRequestLogging();
}
app.UseExceptionHandler();

// Ensure database schema + seed (dev/local resilience)
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<Eseller.Infrastructure.Persistence.EsellerDbContext>();
        await db.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migrate/seed failed. Check SQL Server (EsellerDb) is ONLINE and connection string is correct.");
        throw;
    }
}

// Scalar (Development only)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseStaticFiles();

var uploadsDir = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
if (!Directory.Exists(uploadsDir))
{
    Directory.CreateDirectory(uploadsDir);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsDir),
    RequestPath = "/uploads"
});

app.UseCors("FrontendPolicy");
app.UseRateLimiter();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ─── Endpoints ──────────────────────────────────────────────
app.MapHealthChecks("/health");

app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<NotificationHub>("/hubs/notifications");

// ─── Run ────────────────────────────────────────────────────
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