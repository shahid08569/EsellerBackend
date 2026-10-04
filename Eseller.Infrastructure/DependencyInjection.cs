using Eseller.Application.Abstractions;
using Eseller.Infrastructure.Persistence;
using Eseller.Infrastructure.Services;
using Eseller.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Eseller.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<EsellerDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b =>
                {
                    b.MigrationsAssembly("Eseller.Infrastructure");
                    b.CommandTimeout(180);
                    b.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });

            // Avoid hard-crash on minor model/snapshot drift during local MigrateAsync
            options.ConfigureWarnings(w =>
                w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<EsellerDbContext>());

        // JWT Settings
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        // Email Settings
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));

        // Geocoding Settings
        services.Configure<GeocodingSettings>(configuration.GetSection(GeocodingSettings.SectionName));

        // SMS Settings
        services.Configure<SmsSettings>(configuration.GetSection(SmsSettings.SectionName));

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings are not configured.");

        // File Upload
        services.Configure<FileStorageSettings>(configuration.GetSection(FileStorageSettings.SectionName));

        // JWT Authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        // HttpClient for Geocoding
        services.AddHttpClient<IGeocodingService, BigDataCloudGeocodingService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // HttpClient for Resend transactional email
        services.AddHttpClient("Resend", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // Services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ISmsService, MockSmsService>();
        services.AddScoped<IVerificationTokenService, VerificationTokenService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        return services;
    }
}