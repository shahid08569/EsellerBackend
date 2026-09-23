using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminDashboard.GetPlatformDashboard;

public class GetPlatformDashboardQueryHandler
    : IRequestHandler<GetPlatformDashboardQuery, Result<PlatformDashboardDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPlatformDashboardQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PlatformDashboardDto>> Handle(
        GetPlatformDashboardQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure<PlatformDashboardDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        // 2. Role check — SuperAdmin/Partner only
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PlatformDashboardDto>(
                "Only admins can view platform dashboard.", "FORBIDDEN");

        // ═══════════════════════════════════════════════════════════
        // SHOPS
        // ═══════════════════════════════════════════════════════════
        var totalShops = await _context.Shops.CountAsync(cancellationToken);
        var activeShops = await _context.Shops
            .CountAsync(s => s.IsActive, cancellationToken);

        var pendingShops = await _context.Shopkeepers
            .CountAsync(sk => sk.Status == ShopkeeperApprovalStatus.Pending, cancellationToken);

        // ═══════════════════════════════════════════════════════════
        // PRODUCTS
        // ═══════════════════════════════════════════════════════════
        var totalProducts = await _context.Products
            .IgnoreQueryFilters()
            .CountAsync(p => p.DeletedAt == null, cancellationToken);

        var approvedProducts = await _context.Products
            .IgnoreQueryFilters()
            .CountAsync(p => p.IsApproved && p.DeletedAt == null, cancellationToken);

        var rejectedProducts = await _context.Products
            .IgnoreQueryFilters()
            .CountAsync(p => !p.IsApproved
                          && p.RejectionReason != null
                          && p.DeletedAt == null, cancellationToken);

        var pendingProducts = totalProducts - approvedProducts - rejectedProducts;

        // ═══════════════════════════════════════════════════════════
        // ORDERS
        // ═══════════════════════════════════════════════════════════
        var totalOrders = await _context.OrderRequests.CountAsync(cancellationToken);
        var pendingOrders = await _context.OrderRequests
            .CountAsync(o => o.Status == OrderStatus.Pending, cancellationToken);
        var deliveredOrders = await _context.OrderRequests
            .CountAsync(o => o.Status == OrderStatus.Delivered, cancellationToken);
        var cancelledOrders = await _context.OrderRequests
            .CountAsync(o => o.Status == OrderStatus.Cancelled, cancellationToken);

        // ═══════════════════════════════════════════════════════════
        // REVENUE (Total + Delivered)
        // ═══════════════════════════════════════════════════════════
        var totalRevenue = await _context.OrderRequestItems
            .SumAsync(i => (decimal?)i.TotalPriceSnapshot, cancellationToken) ?? 0m;

        var deliveredOrderIds = await _context.OrderRequests
            .Where(o => o.Status == OrderStatus.Delivered)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var deliveredRevenue = await _context.OrderRequestItems
            .Where(i => deliveredOrderIds.Contains(i.OrderRequestId))
            .SumAsync(i => (decimal?)i.TotalPriceSnapshot, cancellationToken) ?? 0m;

        // ═══════════════════════════════════════════════════════════
        // AFFILIATES
        // ═══════════════════════════════════════════════════════════
        var totalAffiliates = await _context.Affiliates.CountAsync(cancellationToken);
        var activeAffiliates = await _context.Affiliates
            .CountAsync(a => a.IsActive, cancellationToken);

        var totalCommissionsPaid = await _context.AffiliateCommissions
            .Where(c => c.Status == AffiliateCommissionStatus.Paid)
            .SumAsync(c => (decimal?)c.CommissionAmount, cancellationToken) ?? 0m;

        var totalCommissionsPending = await _context.AffiliateCommissions
            .Where(c => c.Status == AffiliateCommissionStatus.Pending
                     || c.Status == AffiliateCommissionStatus.Approved)
            .SumAsync(c => (decimal?)c.CommissionAmount, cancellationToken) ?? 0m;

        // ═══════════════════════════════════════════════════════════
        // WITHDRAWALS
        // ═══════════════════════════════════════════════════════════
        var pendingWithdrawals = await _context.Withdrawals
            .CountAsync(w => w.Status == WithdrawalStatus.Pending, cancellationToken);

        var pendingWithdrawalsAmount = await _context.Withdrawals
            .Where(w => w.Status == WithdrawalStatus.Pending)
            .SumAsync(w => (decimal?)w.Amount, cancellationToken) ?? 0m;

        // ═══════════════════════════════════════════════════════════
        // USERS
        // ═══════════════════════════════════════════════════════════
        var totalCustomers = await _context.Accounts
            .CountAsync(a => a.RoleType == RoleType.User, cancellationToken);

        var totalShopkeepers = await _context.Accounts
            .CountAsync(a => a.RoleType == RoleType.Shopkeeper, cancellationToken);

        // ═══════════════════════════════════════════════════════════
        // RECENT ACTIVITY
        // ═══════════════════════════════════════════════════════════
        var lastOrderAt = await _context.OrderRequests
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => (DateTime?)o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastShopRegisteredAt = await _context.Shops
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => (DateTime?)s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastAffiliateRegisteredAt = await _context.Affiliates
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => (DateTime?)a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // ═══════════════════════════════════════════════════════════
        // BUILD DTO
        // ═══════════════════════════════════════════════════════════
        var dto = new PlatformDashboardDto(
            totalShops,
            activeShops,
            pendingShops,

            totalProducts,
            approvedProducts,
            pendingProducts,
            rejectedProducts,

            totalOrders,
            pendingOrders,
            deliveredOrders,
            cancelledOrders,

            totalRevenue,
            deliveredRevenue,

            totalAffiliates,
            activeAffiliates,
            totalCommissionsPaid,
            totalCommissionsPending,

            pendingWithdrawals,
            pendingWithdrawalsAmount,

            totalCustomers,
            totalShopkeepers,

            lastOrderAt,
            lastShopRegisteredAt,
            lastAffiliateRegisteredAt);

        return Result.Success(dto);
    }
}