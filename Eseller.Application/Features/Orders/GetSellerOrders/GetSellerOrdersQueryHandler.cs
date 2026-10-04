using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Orders.GetSellerOrders;

public class GetSellerOrdersQueryHandler
    : IRequestHandler<GetSellerOrdersQuery, Result<PagedList<SellerOrderListItemDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSellerOrdersQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<SellerOrderListItemDto>>> Handle(
        GetSellerOrdersQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<SellerOrderListItemDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.Shopkeeper)
            return Result.Failure<PagedList<SellerOrderListItemDto>>(
                "Only shopkeepers can list their seller orders.", "FORBIDDEN");

        var shopkeeper = await _context.Shopkeepers
            .AsNoTracking()
            .FirstOrDefaultAsync(sk => sk.AccountId == _currentUserService.AccountId.Value, cancellationToken);

        if (shopkeeper is null)
            return Result.Failure<PagedList<SellerOrderListItemDto>>(
                "Shopkeeper profile not found.", "SHOPKEEPER_NOT_FOUND");

        var shopIds = await _context.Shops
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (shopIds.Count == 0)
        {
            return Result.Success(new PagedList<SellerOrderListItemDto>(
                [], 0, request.PageNumber, request.PageSize));
        }

        var query = _context.SellerOrders
            .AsNoTracking()
            .Where(so => shopIds.Contains(so.ShopId));

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<OrderStatus>(request.Status, true, out var statusFilter))
        {
            query = query.Where(so => so.Status == statusFilter);
        }

        query = query.OrderByDescending(so => so.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        if (page.Count == 0)
        {
            return Result.Success(new PagedList<SellerOrderListItemDto>(
                [], totalCount, request.PageNumber, request.PageSize));
        }

        var sellerOrderIds = page.Select(so => so.Id).ToList();
        var orderRequestIds = page.Select(so => so.OrderRequestId).Distinct().ToList();

        var orderRequests = await _context.OrderRequests
            .AsNoTracking()
            .Where(o => orderRequestIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, cancellationToken);

        var items = await _context.OrderRequestItems
            .AsNoTracking()
            .Where(i => sellerOrderIds.Contains(i.SellerOrderId))
            .ToListAsync(cancellationToken);

        var itemsBySellerOrder = items.GroupBy(i => i.SellerOrderId).ToDictionary(g => g.Key, g => g.ToList());

        var paymentRows = await _context.Payments
            .AsNoTracking()
            .Where(p => orderRequestIds.Contains(p.OrderRequestId))
            .ToListAsync(cancellationToken);
        var payments = paymentRows
            .GroupBy(p => p.OrderRequestId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreatedAt).First());

        var dtos = page.Select(so =>
        {
            var lines = itemsBySellerOrder.GetValueOrDefault(so.Id) ?? [];
            orderRequests.TryGetValue(so.OrderRequestId, out var parent);
            payments.TryGetValue(so.OrderRequestId, out var payment);
            var total = lines.Sum(i => i.TotalPriceSnapshot);
            var profit = Math.Round(total * 0.20m, 2);
            var payLabel = payment is null
                ? "Pending"
                : payment.Status == PaymentStatus.Completed ? "Paid" : payment.Status.ToString();
            var methodLabel = payment is null
                ? "Cash on delivery"
                : payment.Method == PaymentMethod.CashOnDelivery
                    ? "Cash on delivery"
                    : payment.Method.ToString();

            return new SellerOrderListItemDto(
                so.Id,
                so.OrderRequestId,
                so.OrderRequestId.ToString()[..8].ToUpperInvariant(),
                parent?.CustomerName ?? "Customer",
                MaskPhone(parent?.CustomerPhone),
                MaskAddress(parent?.ShippingAddress),
                parent?.City,
                so.Status.ToString(),
                payLabel,
                methodLabel,
                profit,
                total,
                lines.Sum(i => i.Quantity),
                lines.Select(i => new SellerOrderListLineDto(
                    i.Id,
                    i.ProductNameSnapshot,
                    i.Quantity,
                    i.UnitPriceSnapshot,
                    i.TotalPriceSnapshot,
                    null)).ToList(),
                so.CreatedAt);
        }).ToList();

        return Result.Success(new PagedList<SellerOrderListItemDto>(
            dtos, totalCount, request.PageNumber, request.PageSize));
    }

    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return phone;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 4) return "****";
        var prefix = phone.TrimStart().StartsWith('+') ? "+" : "";
        return $"{prefix}{digits[..Math.Min(2, digits.Length)]}*********{digits[^2..]}";
    }

    private static string? MaskAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return address;
        // Hide support/chat channel markers from seller UI
        if (address.StartsWith("CUSTOMER_SUPPORT_", StringComparison.OrdinalIgnoreCase)
            || address.StartsWith("SHOP_", StringComparison.OrdinalIgnoreCase))
            return null;
        if (address.Length <= 12) return address;
        return address[..6] + "****" + address[^4..];
    }
}
