using Eseller.Application.Abstractions;
using Eseller.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Common;

public static class PartnerPermissionChecker
{
    public const string DefaultPartnerPermissions =
        "shops:view,shops:manage,products:view,products:manage,catalog:manage,orders:view,orders:manage,orders:place,customers:manage,finance:view,finance:manage,traffic:view,traffic:manage";

    public static async Task<bool> HasPermissionAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        string requiredPermission,
        CancellationToken cancellationToken = default)
    {
        if (currentUserService.AccountId == null) return false;
        if (currentUserService.RoleType == RoleType.SuperAdmin) return true;
        if (currentUserService.RoleType != RoleType.Partner) return false;

        // Baseline: Any valid partner is inherently allowed to view orders for their own referred shops
        if (string.Equals(requiredPermission, "orders:view", StringComparison.OrdinalIgnoreCase))
            return true;

        var partner = await context.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.AccountId == currentUserService.AccountId.Value, cancellationToken);

        var rawPermissions = !string.IsNullOrWhiteSpace(partner?.Permissions)
            ? partner.Permissions
            : DefaultPartnerPermissions;

        var perms = ParsePermissions(rawPermissions);
        if (perms.Contains("*") || perms.Contains("all")) return true;
        if (perms.Contains(requiredPermission)) return true;

        return false;
    }

    public static HashSet<string> ParsePermissions(string? raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return set;
        try
        {
            if (raw.TrimStart().StartsWith("["))
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw);
                if (list != null)
                {
                    foreach (var p in list)
                    {
                        if (!string.IsNullOrWhiteSpace(p)) set.Add(p.Trim());
                    }
                    return set;
                }
            }
        }
        catch { }

        foreach (var p in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            set.Add(p);
        }
        return set;
    }
}
