using Eseller.Application.Abstractions;
using Eseller.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Common;

public static class PartnerPermissionChecker
{
    public static async Task<bool> HasPermissionAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        string requiredPermission,
        CancellationToken cancellationToken = default)
    {
        if (currentUserService.AccountId == null) return false;
        if (currentUserService.RoleType == RoleType.SuperAdmin) return true;
        if (currentUserService.RoleType != RoleType.Partner) return false;

        var partner = await context.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.AccountId == currentUserService.AccountId.Value, cancellationToken);
        if (partner == null || string.IsNullOrWhiteSpace(partner.Permissions))
            return false;

        var perms = ParsePermissions(partner.Permissions);
        if (perms.Contains("*") || perms.Contains("all")) return true;
        if (perms.Contains(requiredPermission)) return true;

        var prefix = requiredPermission.Split(':')[0];
        if (perms.Contains(prefix)) return true;

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
