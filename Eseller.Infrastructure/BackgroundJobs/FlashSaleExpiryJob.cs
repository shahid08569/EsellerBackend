using Eseller.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eseller.Infrastructure.BackgroundJobs;

public class FlashSaleExpiryJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FlashSaleExpiryJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public FlashSaleExpiryJob(
        IServiceProvider serviceProvider,
        ILogger<FlashSaleExpiryJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FlashSaleExpiryJob started. Interval: {Interval}", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredSalesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired flash sales");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessExpiredSalesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var dateTime = scope.ServiceProvider.GetRequiredService<IDateTime>();

        var now = dateTime.UtcNow;

        // ✅ Sirf wo flash sales jo expire ho gayi hain
        var expiredSales = await context.FlashSales
            .Where(fs => fs.IsActive && fs.EndDate < now)
            .ToListAsync(ct);

        if (expiredSales.Count == 0)
            return;

        foreach (var sale in expiredSales)
        {
            sale.IsActive = false;
        }

        await context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Deactivated {Count} expired flash sales at {Time}",
            expiredSales.Count, now);
    }
}