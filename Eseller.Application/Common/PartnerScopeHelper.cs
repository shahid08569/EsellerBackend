using Eseller.Application.Abstractions;
using Eseller.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Common;

/// <summary>
/// Strict isolation and scoping helper for Partners.
/// When user is SuperAdmin: returns null (no shop filter - SuperAdmin manages all platform shops).
/// When user is Partner: returns only the shops and shopkeepers that registered via this Partner's referral code.
/// </summary>
public static class PartnerScopeHelper
{
    public static async Task<Guid?> GetCurrentPartnerIdAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        if (currentUserService.AccountId == null || currentUserService.RoleType != RoleType.Partner)
            return null;

        var partner = await context.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.AccountId == currentUserService.AccountId.Value, cancellationToken);

        return partner?.Id;
    }

    public static async Task<HashSet<Guid>?> GetPartnerShopIdsAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        CancellationToken cancellationToken = default)
    {
        if (currentUserService.RoleType != RoleType.Partner)
            return null; // SuperAdmin or other -> unrestricted (no partner filter)

        var partnerId = await GetCurrentPartnerIdAsync(context, currentUserService, cancellationToken);
        if (!partnerId.HasValue)
            return new HashSet<Guid>(); // Partner without a profile -> strictly 0 shops

        var shopIds = await context.Shops.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.PartnerId == partnerId.Value && s.DeletedAt == null)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        return new HashSet<Guid>(shopIds);
    }

    public static async Task<bool> IsShopPermittedForPartnerAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserService.RoleType == RoleType.SuperAdmin)
            return true;

        if (currentUserService.RoleType != RoleType.Partner)
            return false;

        var partnerId = await GetCurrentPartnerIdAsync(context, currentUserService, cancellationToken);
        if (!partnerId.HasValue)
            return false;

        return await context.Shops.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(s => s.Id == shopId && s.PartnerId == partnerId.Value, cancellationToken);
    }
}
