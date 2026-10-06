using Eseller.API.Filters;
using Eseller.API.Middleware;
using Eseller.Application;
using Eseller.Application.Abstractions;
using Eseller.Infrastructure;
using Eseller.Infrastructure.BackgroundJobs;
using Eseller.Infrastructure.Persistence;
using Eseller.Infrastructure.Settings;
using Eseller.Infrastructure.SignalR;
using Eseller.Infrastructure.SignalR.Hubs;
using Microsoft.AspNetCore.HttpOverrides;
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
builder.Services.AddControllers(options =>
{
    options.Filters.Add<RequestAbortedExceptionFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust reverse proxy (nginx/Azure) — restrict KnownProxies in hardened deployments
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ─── CORS (config-driven) ───────────────────────────────────
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(corsOrigins)
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

        // Auth-sensitive endpoints: strict limit (5/min)
        if (path.Contains("/auth/login")
            || path.Contains("/auth/register")
            || path.Contains("/auth/forgot-password")
            || path.Contains("/auth/reset-password")
            || path.Contains("/auth/refresh-token")
            || path.Contains("/auth/send-phone-otp")
            || path.Contains("/auth/verify-phone-otp")
            || path.Contains("/auth/upload-document"))
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

app.UseForwardedHeaders();

// ─── Middleware Pipeline ────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseSerilogRequestLogging();
    app.UseHsts();
}
app.UseExceptionHandler();

var seedSettings = app.Configuration.GetSection(SeedSettings.SectionName).Get<SeedSettings>()
    ?? new SeedSettings();

var shouldMigrate = seedSettings.AutoMigrate || app.Environment.IsDevelopment();
// Idempotent: categories/brands/tiers always; SuperAdmin only when password is configured.
// Production.json often has SeedOnStartup=false and is not overwritten by FTP deploy.
var shouldSeed = true;

if (shouldMigrate || shouldSeed)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<EsellerDbContext>();
        if (shouldMigrate)
            await db.Database.MigrateAsync();

        if (shouldSeed)
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migrate/seed failed. Check SQL Server and connection string.");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseStaticFiles();

var uploadsDir = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
if (!Directory.Exists(uploadsDir))
    Directory.CreateDirectory(uploadsDir);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsDir),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        // KYC / verification docs must not be world-readable
        var path = ctx.Context.Request.Path.Value ?? "";
        if (path.Contains("/documents/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.StatusCode = StatusCodes.Status404NotFound;
            ctx.Context.Response.ContentLength = 0;
            ctx.Context.Response.Body = Stream.Null;
        }
    }
});

app.UseCors("FrontendPolicy");
app.UseRateLimiter();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health");

app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<NotificationHub>("/hubs/notifications");

try
{
    Log.Information("Starting Eseller API ({Environment})", app.Environment.EnvironmentName);
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
