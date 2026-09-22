using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Chat.MarkMessageAsRead;

public class MarkMessageAsReadCommandHandler
    : IRequestHandler<MarkMessageAsReadCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public MarkMessageAsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        MarkMessageAsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var message = await _context.ChatMessages
            .FirstOrDefaultAsync(cm => cm.Id == request.MessageId, cancellationToken);

        if (message is null)
            return Result.Failure("Message not found.", "MESSAGE_NOT_FOUND");

        // Verify ownership
        var order = await _context.OrderRequests
            .FirstOrDefaultAsync(o => o.Id == message.OrderRequestId, cancellationToken);

        if (order is null)
            return Result.Failure("Order not found.", "ORDER_NOT_FOUND");

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        if (!isAdmin)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                    cancellationToken);

            if (user is null || order.UserId != user.Id)
                return Result.Failure("You do not have permission.", "FORBIDDEN");
        }

        // Don't mark own messages as read
        if (message.SenderAccountId == _currentUserService.AccountId.Value)
            return Result.Success();

        if (!message.IsRead)
        {
            message.IsRead = true;
            message.ReadAt = _dateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}