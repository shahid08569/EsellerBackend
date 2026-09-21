using Eseller.Application.Abstractions;
using Eseller.Application.Common;
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

        // Find order
        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == request.OrderRequestId, cancellationToken);

        if (order is null)
            return Result.Failure<PagedList<ChatMessageDto>>(
                "Order not found.", "ORDER_NOT_FOUND");

        // Permission check
        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;
        var isCustomer = role == RoleType.User;

        if (!isAdmin)
        {
            if (!isCustomer)
                return Result.Failure<PagedList<ChatMessageDto>>(
                    "You do not have permission.", "FORBIDDEN");

            // Verify customer owns this order
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                    cancellationToken);

            if (user is null || order.UserId != user.Id)
                return Result.Failure<PagedList<ChatMessageDto>>(
                    "You do not have permission.", "FORBIDDEN");
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