using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ChatMessageEntity = Eseller.Domain.Entities.Orders.ChatMessage;

namespace Eseller.Application.Features.Chat.SendChatMessage;

public class SendChatMessageCommandHandler
    : IRequestHandler<SendChatMessageCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public SendChatMessageCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        SendChatMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == request.OrderRequestId, cancellationToken);

        if (order is null)
            return Result.Failure<Guid>("Order not found.", "ORDER_NOT_FOUND");

        // Permission check
        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;
        var isCustomer = role == RoleType.User;

        ChatSenderRole senderRole;

        if (isAdmin)
        {
            senderRole = role == RoleType.SuperAdmin
                ? ChatSenderRole.SuperAdmin
                : ChatSenderRole.Partner;
        }
        else if (isCustomer)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                    cancellationToken);

            if (user is null || order.UserId != user.Id)
                return Result.Failure<Guid>("You do not have permission.", "FORBIDDEN");

            senderRole = ChatSenderRole.User;
        }
        else
        {
            return Result.Failure<Guid>("You do not have permission.", "FORBIDDEN");
        }

        var chatMessage = new ChatMessageEntity
        {
            OrderRequestId = request.OrderRequestId,
            SenderAccountId = _currentUserService.AccountId.Value,
            SenderRole = senderRole,
            Message = request.Message.Trim(),
            SentAt = _dateTime.UtcNow,
            IsRead = false
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(chatMessage.Id);
    }
}