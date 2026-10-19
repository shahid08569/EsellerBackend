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
        var partnerAccountGuid = currentUserService.AccountId ?? Guid.Empty;
        var partnerEntityGuid = partnerId ?? Guid.Empty;

        var partnerShopkeeperIds = await context.Shopkeepers.AsNoTracking()
            .Where(sk => sk.PartnerId == partnerEntityGuid || sk.PartnerId == partnerAccountGuid)
            .Select(sk => sk.Id)
            .ToListAsync(cancellationToken);

        var shopIds = await context.Shops.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.DeletedAt == null && (
                s.PartnerId == partnerEntityGuid ||
                s.PartnerId == partnerAccountGuid ||
                partnerShopkeeperIds.Contains(s.ShopkeeperId)))
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

        var partnerShopIds = await GetPartnerShopIdsAsync(context, currentUserService, cancellationToken);
        return partnerShopIds != null && partnerShopIds.Contains(shopId);
    }
}
