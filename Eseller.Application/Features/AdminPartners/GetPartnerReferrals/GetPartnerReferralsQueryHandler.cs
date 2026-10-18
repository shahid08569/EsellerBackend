using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminPartners.GetPartnerReferrals;

public class GetPartnerReferralsQueryHandler
    : IRequestHandler<GetPartnerReferralsQuery, Result<PartnerReferralsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPartnerReferralsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PartnerReferralsDto>> Handle(
        GetPartnerReferralsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PartnerReferralsDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PartnerReferralsDto>(
                "Only admins and partners can view referrals.", "FORBIDDEN");

        Partner? partner = null;

        if (role == RoleType.Partner)
        {
            partner = await _context.Partners.AsNoTracking()
                .FirstOrDefaultAsync(p => p.AccountId == _currentUserService.AccountId.Value, cancellationToken);
        }
        else if (role == RoleType.SuperAdmin)
        {
            if (request.PartnerId.HasValue && request.PartnerId.Value != Guid.Empty)
            {
                partner = await _context.Partners.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == request.PartnerId.Value, cancellationToken);
            }
            else
            {
                partner = await _context.Partners.AsNoTracking()
                    .OrderBy(p => p.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        if (partner == null)
            return Result.Failure<PartnerReferralsDto>("Partner record not found.", "PARTNER_NOT_FOUND");

        var partnerAccount = await _context.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == partner.AccountId, cancellationToken);

        var refCode = partner.ReferralCode;
        if (string.IsNullOrWhiteSpace(refCode))
        {
            refCode = $"PRT-{partner.Id.ToString("N")[..6].ToUpperInvariant()}";
        }

        var referralLink = $"https://seller.esellerglobal.com/register?ref={Uri.EscapeDataString(refCode)}";
        var alternateLink = $"https://esellerglobal.com/auth/seller-register?ref={Uri.EscapeDataString(refCode)}";

        // Query all shops referred by this partner
        var referredShops = await _context.Shops.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.PartnerId == partner.Id && s.DeletedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var shopkeeperIds = referredShops.Select(s => s.ShopkeeperId).Distinct().ToList();
        var shopkeepers = shopkeeperIds.Count == 0
            ? new Dictionary<Guid, Shopkeeper>()
            : await _context.Shopkeepers.AsNoTracking()
                .Where(sk => shopkeeperIds.Contains(sk.Id))
                .ToDictionaryAsync(sk => sk.Id, cancellationToken);

        var accountIds = shopkeepers.Values.Select(sk => sk.AccountId).Distinct().ToList();
        var accounts = accountIds.Count == 0
            ? new Dictionary<Guid, Account>()
            : await _context.Accounts.AsNoTracking()
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

        var shopIds = referredShops.Select(s => s.Id).ToList();

        // Calculate order counts and sales per shop
        var sellerOrders = shopIds.Count == 0
            ? new List<Eseller.Domain.Entities.Orders.SellerOrder>()
            : await _context.SellerOrders.AsNoTracking()
                .Where(so => shopIds.Contains(so.ShopId))
                .ToListAsync(cancellationToken);

        var sellerOrderIds = sellerOrders.Select(so => so.Id).ToList();

        var itemTotals = sellerOrderIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await _context.OrderRequestItems.AsNoTracking()
                .Where(i => sellerOrderIds.Contains(i.SellerOrderId))
                .GroupBy(i => i.SellerOrderId)
                .Select(g => new { SellerOrderId = g.Key, Total = g.Sum(x => x.TotalPriceSnapshot) })
                .ToDictionaryAsync(x => x.SellerOrderId, x => x.Total, cancellationToken);

        var ordersDict = sellerOrders.GroupBy(so => so.ShopId).ToDictionary(g => g.Key, g => g.Count());
        var salesDict = sellerOrders.GroupBy(so => so.ShopId).ToDictionary(g => g.Key, g => g.Sum(so => itemTotals.GetValueOrDefault(so.Id, 0m)));

        var shopDtos = referredShops.Select(s =>
        {
            shopkeepers.TryGetValue(s.ShopkeeperId, out var keeper);
            Account? acc = null;
            if (keeper != null) accounts.TryGetValue(keeper.AccountId, out acc);

            var statusStr = keeper?.Status.ToString() ?? (s.IsActive ? "Approved" : "Pending");

            ordersDict.TryGetValue(s.Id, out var oCount);
            salesDict.TryGetValue(s.Id, out var sTotal);

            return new PartnerReferredShopDto(
                s.Id,
                s.ShopkeeperId,
                s.Name,
                s.Slug,
                s.LogoUrl,
                s.Phone ?? keeper?.Phone,
                keeper?.Name ?? s.Name,
                acc?.Email ?? string.Empty,
                statusStr,
                s.IsActive,
                s.TotalProducts,
                s.Rating,
                oCount,
                sTotal,
                s.CreatedAt);
        }).ToList();

        var totalShops = shopDtos.Count;
        var activeShops = shopDtos.Count(s => s.IsActive && s.Status == "Approved");
        var pendingShops = shopDtos.Count(s => s.Status == "Pending");
        var suspendedShops = shopDtos.Count(s => s.Status != "Pending" && (!s.IsActive || s.Status == "Rejected" || s.Status == "Suspended"));
        var totalOrders = shopDtos.Sum(s => s.OrdersCount);
        var totalSales = shopDtos.Sum(s => s.TotalSales);

        return Result.Success(new PartnerReferralsDto(
            partner.Id,
            partnerAccount?.Email ?? string.Empty,
            refCode,
            referralLink,
            alternateLink,
            totalShops,
            activeShops,
            pendingShops,
            suspendedShops,
            totalOrders,
            totalSales,
            shopDtos));
    }
}
