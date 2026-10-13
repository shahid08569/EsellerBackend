using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Eseller.Application.Features.Shops;

public class ShopTrafficSummaryDto
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public long TotalVisits { get; set; }
    public int TodayVisits { get; set; }
    public long ThisMonthVisits { get; set; }
    public long LastMonthVisits { get; set; }
    public decimal GrowthRate { get; set; }
    public string TrendDirection { get; set; } = "flat"; // "up" | "down" | "flat"
    public string Range { get; set; } = "30d";
    public List<ShopTrafficPointDto> Timeline { get; set; } = new();
    public List<ShopTrafficMonthDto> MonthlyBreakdown { get; set; } = new();
}

public class ShopTrafficPointDto
{
    public string Date { get; set; } = string.Empty; // YYYY-MM-DD
    public string Label { get; set; } = string.Empty; // Oct 13
    public int Visits { get; set; }
    public int UniqueVisitors { get; set; }
    public bool IsManual { get; set; }
}

public class ShopTrafficMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty; // e.g. "October 2026"
    public long Visits { get; set; }
    public long UniqueVisitors { get; set; }
    public long AutoTrackedVisits { get; set; }
    public long ManualVisits { get; set; }
    public bool IsManual { get; set; }
}

public class AdminShopTrafficItemDto
{
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public decimal Rating { get; set; }
    public bool IsApproved { get; set; }
    public bool IsActive { get; set; }
    public long TotalVisits { get; set; }
    public int TodayVisits { get; set; }
    public long ThisMonthVisits { get; set; }
    public long LastMonthVisits { get; set; }
    public decimal GrowthRate { get; set; }
    public string TrendDirection { get; set; } = "flat";
}

public static class ShopTrafficHelper
{
    public static async Task<ShopTrafficSummaryDto> BuildSummaryAsync(
        Guid shopId,
        string shopName,
        string range,
        IApplicationDbContext context,
        CancellationToken cancellationToken = default,
        string? logoUrl = null,
        DateTime? customStartDate = null,
        DateTime? customEndDate = null,
        bool isForSeller = false)
    {
        var nowUtc = DateTime.UtcNow;
        var todayUtc = nowUtc.Date;
        var firstOfMonthUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthUtc = firstOfMonthUtc.AddMonths(1);
        var firstOfLastMonthUtc = firstOfMonthUtc.AddMonths(-1);

        var totalVisits = await context.ShopTraffics
            .Where(t => t.ShopId == shopId)
            .SumAsync(t => (long)t.VisitCount, cancellationToken);

        var todayVisits = await context.ShopTraffics
            .Where(t => t.ShopId == shopId && t.Date == todayUtc)
            .Select(t => t.VisitCount)
            .FirstOrDefaultAsync(cancellationToken);

        var thisMonthVisits = await context.ShopTraffics
            .Where(t => t.ShopId == shopId && t.Date >= firstOfMonthUtc && t.Date < nextMonthUtc)
            .SumAsync(t => (long)t.VisitCount, cancellationToken);

        var lastMonthVisits = await context.ShopTraffics
            .Where(t => t.ShopId == shopId && t.Date >= firstOfLastMonthUtc && t.Date < firstOfMonthUtc)
            .SumAsync(t => (long)t.VisitCount, cancellationToken);

        decimal growthRate = 0m;
        if (lastMonthVisits > 0)
        {
            growthRate = Math.Round(((decimal)thisMonthVisits - lastMonthVisits) / lastMonthVisits * 100m, 1);
        }
        else if (thisMonthVisits > 0)
        {
            growthRate = 100m;
        }

        string trend = growthRate > 0 ? "up" : (growthRate < 0 ? "down" : "flat");

        // Determine date range for graph timeline: all, 1y, 1m, 1w, custom
        DateTime startDate;
        DateTime endDate = todayUtc;

        var r = (range ?? "30d").Trim().ToLowerInvariant();
        if (r.StartsWith("custom") && r.Contains("_"))
        {
            var parts = r.Split('_');
            if (parts.Length >= 3 &&
                DateTime.TryParse(parts[1], out var pStart) &&
                DateTime.TryParse(parts[2], out var pEnd))
            {
                customStartDate = pStart;
                customEndDate = pEnd;
                r = "custom";
            }
        }

        if (r == "custom" && customStartDate.HasValue)
        {
            startDate = customStartDate.Value.Date;
            endDate = (customEndDate ?? todayUtc).Date;
            if (startDate > endDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }
            if (endDate > todayUtc) endDate = todayUtc;
            if ((endDate - startDate).TotalDays > 730)
            {
                startDate = endDate.AddDays(-730);
            }
        }
        else if (r == "1w" || r == "1week" || r == "7d" || r == "7days")
        {
            startDate = todayUtc.AddDays(-6);
        }
        else if (r == "1m" || r == "1month" || r == "30d" || r == "30days")
        {
            startDate = todayUtc.AddDays(-29);
        }
        else if (r == "1y" || r == "1year" || r == "365d" || r == "365days")
        {
            startDate = todayUtc.AddDays(-364);
        }
        else if (r == "all")
        {
            var minDate = await context.ShopTraffics
                .Where(t => t.ShopId == shopId)
                .Select(t => (DateTime?)t.Date)
                .MinAsync(cancellationToken);
            startDate = minDate?.Date ?? todayUtc.AddDays(-29);
            if ((endDate - startDate).TotalDays > 1095) // max 3 years
            {
                startDate = endDate.AddDays(-1095);
            }
        }
        else if (r == "thismonth")
        {
            startDate = firstOfMonthUtc;
        }
        else if (r == "90d" || r == "90days")
        {
            startDate = todayUtc.AddDays(-89);
        }
        else
        {
            startDate = todayUtc.AddDays(-29);
        }

        var dailyRecords = await context.ShopTraffics
            .Where(t => t.ShopId == shopId && t.Date >= startDate && t.Date <= endDate)
            .ToDictionaryAsync(t => t.Date.Date, cancellationToken);

        var timeline = new List<ShopTrafficPointDto>();
        for (var d = startDate; d <= endDate; d = d.AddDays(1))
        {
            dailyRecords.TryGetValue(d, out var rec);
            timeline.Add(new ShopTrafficPointDto
            {
                Date = d.ToString("yyyy-MM-dd"),
                Label = d.ToString("MMM dd", CultureInfo.InvariantCulture),
                Visits = rec?.VisitCount ?? 0,
                UniqueVisitors = rec?.UniqueVisitors ?? 0,
                IsManual = isForSeller ? false : (rec?.IsManual ?? false)
            });
        }

        // Monthly Breakdown for past 12 months
        var pastYearDate = firstOfMonthUtc.AddMonths(-11);
        var rawMonthly = await context.ShopTraffics
            .Where(t => t.ShopId == shopId && t.Date >= pastYearDate)
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Visits = g.Sum(x => (long)x.VisitCount),
                UniqueVisitors = g.Sum(x => (long)x.UniqueVisitors),
                AutoTrackedVisits = g.Where(x => !x.IsManual).Sum(x => (long)x.VisitCount),
                ManualVisits = g.Where(x => x.IsManual).Sum(x => (long)x.VisitCount),
                IsManual = g.Any(x => x.IsManual)
            })
            .ToListAsync(cancellationToken);

        var monthlyDict = rawMonthly.ToDictionary(x => $"{x.Year}-{x.Month:D2}");
        var monthlyList = new List<ShopTrafficMonthDto>();

        for (int i = 0; i < 12; i++)
        {
            var mDate = firstOfMonthUtc.AddMonths(-i);
            var key = $"{mDate.Year}-{mDate.Month:D2}";
            monthlyDict.TryGetValue(key, out var mData);

            var totalMVisits = mData?.Visits ?? 0;
            monthlyList.Add(new ShopTrafficMonthDto
            {
                Year = mDate.Year,
                Month = mDate.Month,
                MonthName = mDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                Visits = totalMVisits,
                UniqueVisitors = mData?.UniqueVisitors ?? 0,
                AutoTrackedVisits = isForSeller ? totalMVisits : (mData?.AutoTrackedVisits ?? 0),
                ManualVisits = isForSeller ? 0 : (mData?.ManualVisits ?? 0),
                IsManual = isForSeller ? false : (mData?.IsManual ?? false)
            });
        }

        return new ShopTrafficSummaryDto
        {
            ShopId = shopId,
            ShopName = shopName,
            LogoUrl = logoUrl,
            TotalVisits = totalVisits,
            TodayVisits = todayVisits,
            ThisMonthVisits = thisMonthVisits,
            LastMonthVisits = lastMonthVisits,
            GrowthRate = growthRate,
            TrendDirection = trend,
            Range = range ?? "30d",
            Timeline = timeline,
            MonthlyBreakdown = monthlyList
        };
    }
}
