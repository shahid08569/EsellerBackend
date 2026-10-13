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
    private readonly ISignalRNotificationService _signalR;

    public MarkMessageAsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime,
        ISignalRNotificationService signalR)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
        _signalR = signalR;
    }

    public async Task<Result> Handle(
        MarkMessageAsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var message = await _context.Messages
            .FirstOrDefaultAsync(m => m.Id == request.MessageId, cancellationToken);

        if (message is null)
            return Result.Failure("Message not found.", "MESSAGE_NOT_FOUND");

        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == message.ConversationId, cancellationToken);

        if (conversation is null)
            return Result.Failure("Conversation not found.", "ORDER_NOT_FOUND");

        if (!await SupportConversationService.CanAccessAsync(
                _context, _currentUserService, conversation, cancellationToken))
            return Result.Failure("You do not have permission.", "FORBIDDEN");

        if (message.SenderId == _currentUserService.AccountId.Value)
            return Result.Success();

        if (!message.IsRead)
        {
            var readAt = _dateTime.UtcNow;
            message.IsRead = true;
            message.ReadAt = readAt;
            if (message.DeliveredAt == null)
            {
                message.DeliveredAt = readAt;
            }
            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                await _signalR.BroadcastMessageReadAsync(
                    conversation.Id,
                    message.Id,
                    readAt,
                    cancellationToken,
                    notifyAccountIds: new[] { message.SenderId });
            }
            catch
            {
                // best-effort realtime
            }
        }

        return Result.Success();
    }
}
