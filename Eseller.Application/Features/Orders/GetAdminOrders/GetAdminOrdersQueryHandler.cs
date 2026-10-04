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

        if (request.Status.HasValue)
        {
            query = query.Where(o => o.Status == request.Status.Value);
        }

        if (request.ShopId.HasValue && request.ShopId.Value != Guid.Empty)
        {
            var orderIdsForShop = _context.SellerOrders
                .Where(so => so.ShopId == request.ShopId.Value)
                .Select(so => so.OrderRequestId);

            query = query.Where(o => orderIdsForShop.Contains(o.Id));
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
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var userMap = users.ToDictionary(u => u.Id, u => new
        {
            u.Name,
            u.Phone,
            Email = accounts.TryGetValue(u.AccountId, out var acc) ? acc.Email : ""
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
            .Where(s => shopIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var sellerOrderByItem = sellerOrders.ToDictionary(so => so.Id);

        var variantIds = items.Select(i => i.ProductVariantId).Distinct().ToList();
        var variants = await _context.ProductVariants
            .IgnoreQueryFilters()
            .Where(pv => variantIds.Contains(pv.Id))
            .ToDictionaryAsync(pv => pv.Id, cancellationToken);

        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();
        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var itemsByOrder = items.GroupBy(i => i.OrderRequestId).ToDictionary(
            g => g.Key,
            g => g.Select(i =>
            {
                var variant = variants.GetValueOrDefault(i.ProductVariantId);
                var product = variant is null ? null : products.GetValueOrDefault(variant.ProductId);
                var sellerOrder = sellerOrderByItem.GetValueOrDefault(i.SellerOrderId);
                var shop = sellerOrder is null ? null : shops.GetValueOrDefault(sellerOrder.ShopId);

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
                g => string.Join(", ", g.Select(so => shops.TryGetValue(so.ShopId, out var s) ? s.Name : "").Where(n => !string.IsNullOrEmpty(n)).Distinct())
            );

        var primaryShopIdByOrder = sellerOrders
            .GroupBy(so => so.OrderRequestId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(so => (Guid?)so.ShopId).FirstOrDefault()
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
