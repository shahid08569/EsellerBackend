using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Chat.DeleteChatMessage;

public class DeleteChatMessageCommandHandler
    : IRequestHandler<DeleteChatMessageCommand, Result<DeleteChatMessageResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly ISignalRNotificationService _signalR;

    public DeleteChatMessageCommandHandler(
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

    public async Task<Result<DeleteChatMessageResult>> Handle(
        DeleteChatMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<DeleteChatMessageResult>("User is not authenticated.", "UNAUTHENTICATED");

        var callerAccountId = _currentUserService.AccountId.Value;
        var role = _currentUserService.RoleType;
        var isAdmin = role is RoleType.SuperAdmin or RoleType.Partner;
        var now = _dateTime.UtcNow;
        var callerIdStr = callerAccountId.ToString();

        // 1. Try modern Messages table
        var message = await _context.Messages
            .FirstOrDefaultAsync(m => m.Id == request.MessageId, cancellationToken);

        if (message is not null)
        {
            if (request.ForEveryone)
            {
                if (message.SenderId != callerAccountId && !isAdmin)
                    return Result.Failure<DeleteChatMessageResult>("You can only delete your own messages for everyone.", "FORBIDDEN");

                message.IsDeletedForEveryone = true;
                message.DeletedForEveryoneAt = now;
                message.Content = "This message was deleted";
                message.AttachmentUrl = null;
                message.AttachmentFileName = null;
                message.AttachmentContentType = null;
                message.AttachmentSizeBytes = null;

                await _context.SaveChangesAsync(cancellationToken);

                try
                {
                    await _signalR.BroadcastMessageDeletedAsync(
                        message.ConversationId,
                        message.Id,
                        true,
                        cancellationToken);
                }
                catch
                {
                    // Best-effort realtime broadcast
                }

                return Result.Success(new DeleteChatMessageResult(message.Id, message.ConversationId, true));
            }
            else
            {
                // Delete for me
                var currentDeletedList = (message.DeletedForAccountIds ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                if (!currentDeletedList.Contains(callerIdStr, StringComparer.OrdinalIgnoreCase))
                {
                    currentDeletedList.Add(callerIdStr);
                    message.DeletedForAccountIds = string.Join(",", currentDeletedList);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                return Result.Success(new DeleteChatMessageResult(message.Id, message.ConversationId, false));
            }
        }

        // 2. Try legacy ChatMessages table
        var legacy = await _context.ChatMessages
            .FirstOrDefaultAsync(cm => cm.Id == request.MessageId, cancellationToken);

        if (legacy is not null)
        {
            if (request.ForEveryone)
            {
                if (legacy.SenderAccountId != callerAccountId && !isAdmin)
                    return Result.Failure<DeleteChatMessageResult>("You can only delete your own messages for everyone.", "FORBIDDEN");

                legacy.IsDeletedForEveryone = true;
                legacy.DeletedForEveryoneAt = now;
                legacy.Message = "This message was deleted";

                await _context.SaveChangesAsync(cancellationToken);

                try
                {
                    await _signalR.BroadcastMessageDeletedAsync(
                        legacy.OrderRequestId,
                        legacy.Id,
                        true,
                        cancellationToken);
                }
                catch
                {
                    // Best-effort realtime broadcast
                }

                return Result.Success(new DeleteChatMessageResult(legacy.Id, legacy.OrderRequestId, true));
            }
            else
            {
                // Delete for me
                var currentDeletedList = (legacy.DeletedForAccountIds ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                if (!currentDeletedList.Contains(callerIdStr, StringComparer.OrdinalIgnoreCase))
                {
                    currentDeletedList.Add(callerIdStr);
                    legacy.DeletedForAccountIds = string.Join(",", currentDeletedList);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                return Result.Success(new DeleteChatMessageResult(legacy.Id, legacy.OrderRequestId, false));
            }
        }

        return Result.Failure<DeleteChatMessageResult>("Message not found.", "MESSAGE_NOT_FOUND");
    }
}
