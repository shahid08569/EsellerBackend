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
builder.Services.AddMemoryCache();
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
        // Normalize trailing slashes / case so www vs apex never silently fail.
        var allowed = new HashSet<string>(
            corsOrigins
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => o.Trim().TrimEnd('/')),
            StringComparer.OrdinalIgnoreCase);

        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin)) return false;
                return allowed.Contains(origin.Trim().TrimEnd('/'));
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// ─── Rate Limiting (Standard OWASP Tiered Limits) ────────────
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var partitionKey = context.User.Identity?.Name
                           ?? context.Connection.RemoteIpAddress?.ToString()
                           ?? "anonymous";

        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Sensitive OTP / Password recovery: 5 per 2 minutes
        if (path.Contains("/auth/send-phone-otp")
            || path.Contains("/auth/verify-phone-otp")
            || path.Contains("/auth/forgot-password")
            || path.Contains("/auth/reset-password"))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"otp-{partitionKey}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(2),
                    QueueLimit = 0
                });
        }

        // Standard Auth endpoints (login, register, token refresh): 20 per minute
        if (path.Contains("/auth/login")
            || path.Contains("/auth/register")
            || path.Contains("/auth/refresh-token")
            || path.Contains("/auth/upload-document"))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"auth-{partitionKey}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 2
                });
        }

        // General API endpoints: 120 per minute with small queue buffer
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 5
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
app.UseMiddleware<SecurityHeadersMiddleware>();

// ─── Middleware Pipeline ────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseSerilogRequestLogging(opt =>
    {
        opt.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (httpContext.Request.Path.StartsWithSegments("/health"))
                return Serilog.Events.LogEventLevel.Verbose;
            return ex != null || httpContext.Response.StatusCode >= 500
                ? Serilog.Events.LogEventLevel.Error
                : httpContext.Response.StatusCode >= 400
                    ? Serilog.Events.LogEventLevel.Warning
                    : Serilog.Events.LogEventLevel.Information;
        };
    });
    app.UseHsts();
}
app.UseExceptionHandler();

var seedSettings = app.Configuration.GetSection(SeedSettings.SectionName).Get<SeedSettings>()
    ?? new SeedSettings();

// Idempotent EF migrations — Production.json often has AutoMigrate=false and is not overwritten by FTP.
var shouldMigrate = true;
// Idempotent: categories/brands/tiers always; SuperAdmin only when password is configured.
var shouldSeed = true;

if (shouldMigrate || shouldSeed)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<EsellerDbContext>();
        if (shouldMigrate)
        {
            try
            {
                await db.Database.MigrateAsync();
            }
            catch (Exception migrateEx)
            {
                logger.LogError(migrateEx, "EF MigrateAsync failed — continuing with raw SQL ensures.");
            }
        }

        // Always ensure Product.ReviewCount (even if MigrateAsync failed / raced).
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH(N'Products', N'ReviewCount') IS NULL
                BEGIN
                    ALTER TABLE [Products] ADD [ReviewCount] int NOT NULL
                        CONSTRAINT [DF_Products_ReviewCount] DEFAULT 0;
                END
                """);
            logger.LogInformation("Ensured Products.ReviewCount column exists.");
        }
        catch (Exception reviewCountEx)
        {
            logger.LogError(reviewCountEx, "Failed to ensure Products.ReviewCount column.");
        }

        // Always ensure Wallet Pending/Available earnings (Delivered status credit needs these).
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH(N'Wallets', N'PendingEarnings') IS NULL
                BEGIN
                    ALTER TABLE [Wallets] ADD [PendingEarnings] decimal(18,2) NOT NULL
                        CONSTRAINT [DF_Wallets_PendingEarnings] DEFAULT 0;
                END

                IF COL_LENGTH(N'Wallets', N'AvailableEarnings') IS NULL
                BEGIN
                    ALTER TABLE [Wallets] ADD [AvailableEarnings] decimal(18,2) NOT NULL
                        CONSTRAINT [DF_Wallets_AvailableEarnings] DEFAULT 0;
                END
                """);
            logger.LogInformation("Ensured Wallets.PendingEarnings / AvailableEarnings columns exist.");
        }
        catch (Exception walletColsEx)
        {
            logger.LogError(walletColsEx, "Failed to ensure Wallets earnings columns.");
        }

        // Ensure chat edit & delete columns on Messages and ChatMessages tables
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH(N'Messages', N'IsEdited') IS NULL
                BEGIN
                    ALTER TABLE [Messages] ADD [IsEdited] bit NOT NULL CONSTRAINT [DF_Messages_IsEdited] DEFAULT 0;
                END
                IF COL_LENGTH(N'Messages', N'EditedAt') IS NULL
                BEGIN
                    ALTER TABLE [Messages] ADD [EditedAt] datetime2 NULL;
                END
                IF COL_LENGTH(N'Messages', N'IsDeletedForEveryone') IS NULL
                BEGIN
                    ALTER TABLE [Messages] ADD [IsDeletedForEveryone] bit NOT NULL CONSTRAINT [DF_Messages_IsDeletedForEveryone] DEFAULT 0;
                END
                IF COL_LENGTH(N'Messages', N'DeletedForEveryoneAt') IS NULL
                BEGIN
                    ALTER TABLE [Messages] ADD [DeletedForEveryoneAt] datetime2 NULL;
                END
                IF COL_LENGTH(N'Messages', N'DeletedForAccountIds') IS NULL
                BEGIN
                    ALTER TABLE [Messages] ADD [DeletedForAccountIds] nvarchar(max) NULL;
                END

                IF COL_LENGTH(N'ChatMessages', N'IsEdited') IS NULL
                BEGIN
                    ALTER TABLE [ChatMessages] ADD [IsEdited] bit NOT NULL CONSTRAINT [DF_ChatMessages_IsEdited] DEFAULT 0;
                END
                IF COL_LENGTH(N'ChatMessages', N'EditedAt') IS NULL
                BEGIN
                    ALTER TABLE [ChatMessages] ADD [EditedAt] datetime2 NULL;
                END
                IF COL_LENGTH(N'ChatMessages', N'IsDeletedForEveryone') IS NULL
                BEGIN
                    ALTER TABLE [ChatMessages] ADD [IsDeletedForEveryone] bit NOT NULL CONSTRAINT [DF_ChatMessages_IsDeletedForEveryone] DEFAULT 0;
                END
                IF COL_LENGTH(N'ChatMessages', N'DeletedForEveryoneAt') IS NULL
                BEGIN
                    ALTER TABLE [ChatMessages] ADD [DeletedForEveryoneAt] datetime2 NULL;
                END
                IF COL_LENGTH(N'ChatMessages', N'DeletedForAccountIds') IS NULL
                BEGIN
                    ALTER TABLE [ChatMessages] ADD [DeletedForAccountIds] nvarchar(max) NULL;
                END
                """);
            logger.LogInformation("Ensured chat edit and delete columns on Messages/ChatMessages.");
        }
        catch (Exception chatColsEx)
        {
            logger.LogError(chatColsEx, "Failed to ensure chat edit and delete columns.");
        }

        // Belt-and-suspenders: banner content columns (migration historically lacked [Migration] attr).
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('HomepageBanners', 'Title') IS NULL
                ALTER TABLE [HomepageBanners] ADD [Title] nvarchar(200) NULL;
            IF COL_LENGTH('HomepageBanners', 'Subtitle') IS NULL
                ALTER TABLE [HomepageBanners] ADD [Subtitle] nvarchar(200) NULL;
            IF COL_LENGTH('HomepageBanners', 'Description') IS NULL
                ALTER TABLE [HomepageBanners] ADD [Description] nvarchar(1000) NULL;
            IF COL_LENGTH('HomepageBanners', 'Price') IS NULL
                ALTER TABLE [HomepageBanners] ADD [Price] decimal(18,2) NULL;
            IF COL_LENGTH('HomepageBanners', 'OriginalPrice') IS NULL
                ALTER TABLE [HomepageBanners] ADD [OriginalPrice] decimal(18,2) NULL;
            IF COL_LENGTH('HomepageBanners', 'ButtonText') IS NULL
                ALTER TABLE [HomepageBanners] ADD [ButtonText] nvarchar(100) NULL;
            IF COL_LENGTH('HomepageBanners', 'BackgroundColor') IS NULL
                ALTER TABLE [HomepageBanners] ADD [BackgroundColor] nvarchar(300) NULL;
            IF COL_LENGTH('HomepageBanners', 'TextColor') IS NULL
                ALTER TABLE [HomepageBanners] ADD [TextColor] nvarchar(50) NULL;
            IF COL_LENGTH('HomepageBanners', 'AvailableSizes') IS NULL
                ALTER TABLE [HomepageBanners] ADD [AvailableSizes] nvarchar(100) NULL;

            IF OBJECT_ID(N'[PaymentShowcaseLogos]', N'U') IS NULL
            BEGIN
                CREATE TABLE [PaymentShowcaseLogos] (
                    [Id] uniqueidentifier NOT NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [ImageUrl] nvarchar(500) NOT NULL,
                    [SortOrder] int NOT NULL CONSTRAINT [DF_PaymentShowcaseLogos_SortOrder] DEFAULT 0,
                    [IsActive] bit NOT NULL CONSTRAINT [DF_PaymentShowcaseLogos_IsActive] DEFAULT 1,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_PaymentShowcaseLogos] PRIMARY KEY ([Id])
                );
                CREATE NONCLUSTERED INDEX [IX_PaymentShowcaseLogos_IsActive] ON [PaymentShowcaseLogos]([IsActive]);
                CREATE NONCLUSTERED INDEX [IX_PaymentShowcaseLogos_SortOrder] ON [PaymentShowcaseLogos]([SortOrder]);
            END
            """);

        if (shouldSeed)
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync();
        }
    }
    catch (Exception ex)
    {
        // Never crash the host on seed/migrate hiccups. If the process fails to start,
        // MonsterASP/IIS returns bare 503 HTML with no CORS headers — browsers then
        // report misleading "blocked by CORS policy" errors on www.esellerglobal.com.
        logger.LogError(ex, "Database migrate/seed failed. API will still start; check SQL Server and connection string.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// CORS must run early so preflight + error responses still get Access-Control-* headers.
app.UseCors("FrontendPolicy");

app.UseStaticFiles();

var uploadsDir = Eseller.Infrastructure.Services.UploadsPathResolver.Resolve(
    app.Environment, app.Configuration);
Log.Information("Serving uploads from {UploadsDir}", uploadsDir);

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
            return;
        }

        // Treat empty placeholders as missing so UIs show fallbacks instead of blank images
        try
        {
            var fileInfo = ctx.File;
            if (fileInfo is { Exists: true, Length: <= 0 })
            {
                ctx.Context.Response.StatusCode = StatusCodes.Status404NotFound;
                ctx.Context.Response.ContentLength = 0;
                ctx.Context.Response.Body = Stream.Null;
            }
        }
        catch { /* ignore */ }
    }
});

app.UseRateLimiter();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health");

// Diagnostics: confirms uploads folder on MonsterASP (no secrets).
app.MapGet("/health/uploads", () =>
{
    var exists = Directory.Exists(uploadsDir);
    var fileCount = 0;
    var zeroByteCount = 0;
    var nonEmptyCount = 0;
    long totalBytes = 0;
    var samples = new List<object>();
    try
    {
        if (exists)
        {
            foreach (var path in Directory.EnumerateFiles(uploadsDir, "*", SearchOption.AllDirectories))
            {
                fileCount++;
                long len = 0;
                try { len = new FileInfo(path).Length; } catch { /* ignore */ }
                if (len <= 0) zeroByteCount++;
                else
                {
                    nonEmptyCount++;
                    totalBytes += len;
                    if (samples.Count < 5)
                    {
                        var rel = Path.GetRelativePath(uploadsDir, path).Replace('\\', '/');
                        samples.Add(new { path = "/uploads/" + rel, bytes = len });
                    }
                }
            }
        }
    }
    catch { /* ignore */ }

    return Results.Ok(new
    {
        uploadsDir,
        exists,
        fileCount,
        nonEmptyCount,
        zeroByteCount,
        totalBytes,
        samples,
        contentRoot = app.Environment.ContentRootPath
    });
});

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
