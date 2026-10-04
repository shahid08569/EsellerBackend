using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Chat.GetChatMessages;

public class GetChatMessagesQueryHandler
    : IRequestHandler<GetChatMessagesQuery, Result<PagedList<ChatMessageDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetChatMessagesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<ChatMessageDto>>> Handle(
        GetChatMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<ChatMessageDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        // Find order — or create shop↔admin support channel keyed by Shop.Id
        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == request.OrderRequestId, cancellationToken);

        if (order is null || order.UserId == Guid.Empty)
        {
            order = await ShopSupportChannel.EnsureAsync(
                _context,
                request.OrderRequestId,
                DateTime.UtcNow,
                cancellationToken);

            if (order is null)
                return Result.Failure<PagedList<ChatMessageDto>>(
                    "Order or shop support channel not found.", "ORDER_NOT_FOUND");
        }

        // Permission check
        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;
        var isCustomer = role == RoleType.User;
        var isShopkeeper = role == RoleType.Shopkeeper;

        if (isShopkeeper)
        {
            var shopkeeper = await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.AccountId == _currentUserService.AccountId.Value, cancellationToken);
            if (shopkeeper is null)
                return Result.Failure<PagedList<ChatMessageDto>>("You do not have permission.", "FORBIDDEN");

            var shops = await _context.Shops
                .IgnoreQueryFilters()
                .Where(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            // Seller may only read Super Admin shop-support threads.
            var ownsSupport = shops.Contains(order.Id)
                || (!string.IsNullOrWhiteSpace(order.ShippingAddress)
                    && shops.Any(id =>
                        order.ShippingAddress!.Equals($"SHOP_SUPPORT_{id}", StringComparison.OrdinalIgnoreCase)));

            if (!ownsSupport)
                return Result.Failure<PagedList<ChatMessageDto>>(
                    "You can only open Super Admin support chat.", "FORBIDDEN");
        }
        else if (!isAdmin)
        {
            if (!isCustomer)
                return Result.Failure<PagedList<ChatMessageDto>>(
                    "You do not have permission.", "FORBIDDEN");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                    cancellationToken);

            if (user is null || order.UserId != user.Id)
                return Result.Failure<PagedList<ChatMessageDto>>(
                    "You do not have permission.", "FORBIDDEN");

            var supportMarker = $"CUSTOMER_SUPPORT_{user.Id}";
            if (!string.Equals(order.ShippingAddress, supportMarker, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(order.ShippingAddress, "Live Support Inquiry", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<PagedList<ChatMessageDto>>(
                    "You can only open Super Admin support chat.", "FORBIDDEN");
            }
        }

        var query = _context.ChatMessages
            .Where(cm => cm.OrderRequestId == request.OrderRequestId)
            .OrderBy(cm => cm.SentAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var messages = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(cm => new ChatMessageDto(
                cm.Id,
                cm.OrderRequestId,
                cm.SenderAccountId,
                cm.SenderRole.ToString(),
                cm.Message,
                cm.IsRead,
                cm.ReadAt,
                cm.DeliveredAt,
                cm.SentAt))
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ChatMessageDto>(
            messages, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}