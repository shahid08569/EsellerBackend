using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Orders.GetAdminOrders;

public class GetAdminOrdersQueryHandler
    : IRequestHandler<GetAdminOrdersQuery, Result<PagedList<AdminOrderListItemDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetAdminOrdersQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<AdminOrderListItemDto>>> Handle(
        GetAdminOrdersQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<AdminOrderListItemDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<AdminOrderListItemDto>>(
                "Only admins can view platform orders.", "FORBIDDEN");

        // Commerce orders only — exclude chat/support channel OrderRequests
        var query = _context.OrderRequests
            .Where(o => o.ShippingAddress == null
                || (!o.ShippingAddress.StartsWith("SHOP_SUPPORT_")
                    && !o.ShippingAddress.StartsWith("SHOP_CHAT_")
                    && !o.ShippingAddress.StartsWith("CUSTOMER_SUPPORT_")
                    && o.ShippingAddress != "Live Support Inquiry"))
            .Where(o => _context.SellerOrders.Any(so => so.OrderRequestId == o.Id)
                        || _context.OrderRequestItems.Any(i => i.OrderRequestId == o.Id));

        // Strict isolation: Partner can ONLY see orders for shops that joined via their own referral link
        List<Guid>? partnerShopIdList = null;
        List<Guid>? scopedShopIds = null;

        if (role == RoleType.Partner)
        {
            var partnerShopIdSet = await PartnerScopeHelper.GetPartnerShopIdsAsync(_context, _currentUserService, cancellationToken);
            partnerShopIdList = partnerShopIdSet != null ? partnerShopIdSet.ToList() : new List<Guid>();

            // Strict isolation: if partner has no referred shops, return empty list immediately
            if (partnerShopIdList.Count == 0)
            {
                return Result.Success(new PagedList<AdminOrderListItemDto>(
                    new List<AdminOrderListItemDto>(), 0, request.PageNumber, request.PageSize));
            }

            if (request.ShopId.HasValue && request.ShopId.Value != Guid.Empty)
            {
                if (!partnerShopIdList.Contains(request.ShopId.Value))
                {
                    // Partner requested a shop outside their referral scope
                    return Result.Success(new PagedList<AdminOrderListItemDto>(
                        new List<AdminOrderListItemDto>(), 0, request.PageNumber, request.PageSize));
                }
                scopedShopIds = new List<Guid> { request.ShopId.Value };
            }
            else
            {
                scopedShopIds = partnerShopIdList;
            }
        }
        else if (role == RoleType.SuperAdmin)
        {
            if (request.ShopId.HasValue && request.ShopId.Value != Guid.Empty)
            {
                scopedShopIds = new List<Guid> { request.ShopId.Value };
            }
        }

        if (scopedShopIds != null && scopedShopIds.Count > 0)
        {
            // 1. Direct seller orders for these shops
            var directOrderIds = await _context.SellerOrders.AsNoTracking()
                .Where(so => scopedShopIds.Contains(so.ShopId))
                .Select(so => so.OrderRequestId)
                .Distinct()
                .ToListAsync(cancellationToken);

            // 2. Orders from OrderRequestItems pointing to products in these shops
            var shopProductIds = await _context.Products.IgnoreQueryFilters().AsNoTracking()
                .Where(p => scopedShopIds.Contains(p.ShopId))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            List<Guid> itemOrderIds = new();
            if (shopProductIds.Count > 0)
            {
                var shopVariantIds = await _context.ProductVariants.IgnoreQueryFilters().AsNoTracking()
                    .Where(v => shopProductIds.Contains(v.ProductId))
                    .Select(v => v.Id)
                    .ToListAsync(cancellationToken);

                if (shopVariantIds.Count > 0)
                {
                    itemOrderIds = await _context.OrderRequestItems.AsNoTracking()
                        .Where(i => shopVariantIds.Contains(i.ProductVariantId))
                        .Select(i => i.OrderRequestId)
                        .Distinct()
                        .ToListAsync(cancellationToken);
                }
            }

            var allowedOrderIds = directOrderIds.Union(itemOrderIds).Distinct().ToList();

            if (allowedOrderIds.Count == 0)
            {
                return Result.Success(new PagedList<AdminOrderListItemDto>(
                    new List<AdminOrderListItemDto>(), 0, request.PageNumber, request.PageSize));
            }

            query = query.Where(o => allowedOrderIds.Contains(o.Id));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(o => o.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(o =>
                (o.CustomerName != null && o.CustomerName.ToLower().Contains(search)) ||
                (o.CustomerPhone != null && o.CustomerPhone.Contains(search)) ||
                (o.City != null && o.City.ToLower().Contains(search)) ||
                (o.ShippingAddress != null && o.ShippingAddress.ToLower().Contains(search)) ||
                o.Id.ToString().Contains(search));
        }

        // Newest orders first
        query = query.OrderByDescending(o => o.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var orderList = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var orderIds = orderList.Select(o => o.Id).ToList();
        var userIds = orderList.Select(o => o.UserId).Distinct().ToList();

        // Load users & accounts for customer contact info
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        var accountIds = users.Select(u => u.AccountId).Distinct().ToList();
        var accounts = await _context.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        var accountMap = accounts
            .GroupBy(a => a.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var userMap = users
            .GroupBy(u => u.Id)
            .ToDictionary(g => g.Key, g =>
            {
                var u = g.First();
                return new
                {
                    u.Name,
                    u.Phone,
                    Email = accountMap.TryGetValue(u.AccountId, out var acc) ? acc.Email : ""
                };
            });

        // Load order items
        var items = await _context.OrderRequestItems
            .Where(i => orderIds.Contains(i.OrderRequestId))
            .ToListAsync(cancellationToken);

        // Load seller orders to get shops
        var sellerOrders = await _context.SellerOrders
            .Where(so => orderIds.Contains(so.OrderRequestId))
            .ToListAsync(cancellationToken);

        var shopIds = sellerOrders.Select(so => so.ShopId).Distinct().ToList();
        var shops = await _context.Shops
            .IgnoreQueryFilters()
            .Where(s => shopIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        var shopMap = shops
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var sellerOrderByItem = sellerOrders
            .GroupBy(so => so.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var variantIds = items.Select(i => i.ProductVariantId).Distinct().ToList();
        var variants = await _context.ProductVariants
            .IgnoreQueryFilters()
            .Where(pv => variantIds.Contains(pv.Id))
            .ToListAsync(cancellationToken);

        var variantMap = variants
            .GroupBy(pv => pv.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var productIds = variants.Select(v => v.ProductId).Distinct().ToList();
        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var productMap = products
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var itemsByOrder = items.GroupBy(i => i.OrderRequestId).ToDictionary(
            g => g.Key,
            g => g.Where(i =>
            {
                if (partnerShopIdList == null) return true;
                var sellerOrder = sellerOrderByItem.GetValueOrDefault(i.SellerOrderId);
                if (sellerOrder != null && partnerShopIdList.Contains(sellerOrder.ShopId)) return true;
                var variant = variantMap.GetValueOrDefault(i.ProductVariantId);
                var product = variant is null ? null : productMap.GetValueOrDefault(variant.ProductId);
                return product != null && partnerShopIdList.Contains(product.ShopId);
            }).Select(i =>
            {
                var variant = variantMap.GetValueOrDefault(i.ProductVariantId);
                var product = variant is null ? null : productMap.GetValueOrDefault(variant.ProductId);
                var sellerOrder = sellerOrderByItem.GetValueOrDefault(i.SellerOrderId);
                var shop = sellerOrder is null ? null : shopMap.GetValueOrDefault(sellerOrder.ShopId);

                return new OrderRequestItemDto(
                    i.Id,
                    product?.Id ?? Guid.Empty,
                    i.ProductVariantId,
                    shop?.Id ?? Guid.Empty,
                    shop?.Name ?? string.Empty,
                    shop?.Slug ?? string.Empty,
                    i.ProductNameSnapshot,
                    variant?.Sku ?? string.Empty,
                    i.VariantAttributesSnapshot,
                    i.Quantity,
                    i.UnitPriceSnapshot,
                    i.TotalPriceSnapshot,
                    i.PriceAtOrder,
                    variant?.StockQty);
            }).ToList()
        );

        var shopsByOrder = sellerOrders
            .GroupBy(so => so.OrderRequestId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.Where(so => partnerShopIdList == null || partnerShopIdList.Contains(so.ShopId)).Select(so => shopMap.TryGetValue(so.ShopId, out var s) ? s.Name : "").Where(n => !string.IsNullOrEmpty(n)).Distinct())
            );

        var primaryShopIdByOrder = sellerOrders
            .GroupBy(so => so.OrderRequestId)
            .ToDictionary(
                g => g.Key,
                g => g.Where(so => partnerShopIdList == null || partnerShopIdList.Contains(so.ShopId)).Select(so => (Guid?)so.ShopId).FirstOrDefault()
                    ?? g.Select(so => (Guid?)so.ShopId).FirstOrDefault()
            );

        var paymentRows = await _context.Payments
            .AsNoTracking()
            .Where(p => orderIds.Contains(p.OrderRequestId))
            .ToListAsync(cancellationToken);
        var payments = paymentRows
            .GroupBy(p => p.OrderRequestId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreatedAt).First());

        var dtos = orderList.Select(o =>
        {
            var orderItems = itemsByOrder.GetValueOrDefault(o.Id) ?? new List<OrderRequestItemDto>();
            userMap.TryGetValue(o.UserId, out var userInfo);
            payments.TryGetValue(o.Id, out var payment);

            var custName = !string.IsNullOrWhiteSpace(o.CustomerName) ? o.CustomerName : (userInfo?.Name ?? "Customer");
            var custPhone = !string.IsNullOrWhiteSpace(o.CustomerPhone) ? o.CustomerPhone : (userInfo?.Phone ?? "");
            var custEmail = userInfo?.Email ?? "";
            var shopName = shopsByOrder.GetValueOrDefault(o.Id) ?? "Platform Order";
            if (string.IsNullOrWhiteSpace(shopName)) shopName = "Platform Order";

            var totalAmount = orderItems.Count > 0 ? orderItems.Sum(i => i.TotalPriceSnapshot) : 0m;
            var totalItems = orderItems.Count > 0 ? orderItems.Sum(i => i.Quantity) : 0;
            var payStatus = payment is null
                ? "Pending"
                : payment.Status == PaymentStatus.Completed ? "Paid" : payment.Status.ToString();
            var payMethod = payment is null
                ? "Cash on delivery"
                : payment.Method == PaymentMethod.CashOnDelivery
                    ? "Cash on delivery"
                    : payment.Method.ToString();

            return new AdminOrderListItemDto(
                o.Id,
                o.Id.ToString()[..8].ToUpper(),
                o.UserId,
                custName,
                custPhone,
                custEmail,
                o.ShippingAddress ?? "",
                o.City ?? "",
                o.Status.ToString(),
                (int)o.Status,
                payStatus,
                payMethod,
                totalAmount,
                totalItems,
                shopName,
                primaryShopIdByOrder.GetValueOrDefault(o.Id),
                o.CreatedAt,
                orderItems);
        }).ToList();

        var pagedList = new PagedList<AdminOrderListItemDto>(
            dtos, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}
