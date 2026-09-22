using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Chat.GetConversations;

public class GetConversationsQueryHandler
    : IRequestHandler<GetConversationsQuery, Result<PagedList<ConversationDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetConversationsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<ConversationDto>>> Handle(
        GetConversationsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<ConversationDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        // Only admin
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<ConversationDto>>(
                "Only admins can view conversations.", "FORBIDDEN");

        // Get orders with chat messages
        var orderIdsWithChat = await _context.ChatMessages
            .Select(cm => cm.OrderRequestId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var query = _context.OrderRequests
            .Where(o => orderIdsWithChat.Contains(o.Id))
            .OrderByDescending(o => o.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var orderIdList = orders.Select(o => o.Id).ToList();

        // Load users
        var userIds = orders.Select(o => o.UserId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var accountIds = users.Values.Select(u => u.AccountId).Distinct().ToList();
        var accounts = await _context.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        // Load items for total
        var allItems = await _context.OrderRequestItems
            .Where(i => orderIdList.Contains(i.OrderRequestId))
            .ToListAsync(cancellationToken);

        var itemsByOrder = allItems
            .GroupBy(i => i.OrderRequestId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load latest chat message per order
        var allMessages = await _context.ChatMessages
            .Where(cm => orderIdList.Contains(cm.OrderRequestId))
            .OrderByDescending(cm => cm.SentAt)
            .ToListAsync(cancellationToken);

        var messagesByOrder = allMessages
            .GroupBy(cm => cm.OrderRequestId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var conversations = new List<ConversationDto>();

        foreach (var order in orders)
        {
            var user = users.GetValueOrDefault(order.UserId);
            var account = user is null ? null : accounts.GetValueOrDefault(user.AccountId);
            var orderItems = itemsByOrder.GetValueOrDefault(order.Id, new List<Domain.Entities.Orders.OrderRequestItem>());
            var messages = messagesByOrder.GetValueOrDefault(order.Id, new List<Domain.Entities.Orders.ChatMessage>());

            var lastMessage = messages.FirstOrDefault();
            var unreadCount = messages.Count(m => !m.IsRead && m.SenderRole == ChatSenderRole.User);

            conversations.Add(new ConversationDto(
                order.Id,
                order.UserId,
                user?.Name ?? string.Empty,
                account?.Email ?? string.Empty,
                order.Status.ToString(),
                orderItems.Sum(i => i.TotalPriceSnapshot),
                orderItems.Sum(i => i.Quantity),
                lastMessage?.Message,
                lastMessage?.SenderAccountId,
                lastMessage?.SentAt,
                unreadCount,
                order.CreatedAt));
        }

        var pagedList = new PagedList<ConversationDto>(
            conversations, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}