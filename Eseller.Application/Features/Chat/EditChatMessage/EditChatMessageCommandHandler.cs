using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Chat.EditChatMessage;

public class EditChatMessageCommandHandler
    : IRequestHandler<EditChatMessageCommand, Result<EditChatMessageResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly ISignalRNotificationService _signalR;

    public EditChatMessageCommandHandler(
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

    public async Task<Result<EditChatMessageResult>> Handle(
        EditChatMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<EditChatMessageResult>("User is not authenticated.", "UNAUTHENTICATED");

        var callerAccountId = _currentUserService.AccountId.Value;
        var role = _currentUserService.RoleType;
        var isAdmin = role is RoleType.SuperAdmin or RoleType.Partner;
        var text = (request.NewContent ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(text))
            return Result.Failure<EditChatMessageResult>("Message content cannot be empty.", "VALIDATION_ERROR");

        var now = _dateTime.UtcNow;

        // 1. Try modern Messages table
        var message = await _context.Messages
            .FirstOrDefaultAsync(m => m.Id == request.MessageId, cancellationToken);

        if (message is not null)
        {
            if (message.IsDeletedForEveryone)
                return Result.Failure<EditChatMessageResult>("Cannot edit a deleted message.", "MESSAGE_DELETED");

            if (message.SenderId != callerAccountId)
                return Result.Failure<EditChatMessageResult>("You can only edit your own messages.", "FORBIDDEN");

            var replyPrefixMatch = System.Text.RegularExpressions.Regex.Match(message.Content, @"^(\[reply:[^:]+:[^\]]+\][\s\S]*?\[/reply\]\s*)");
            if (replyPrefixMatch.Success && !text.StartsWith("[reply:"))
            {
                message.Content = replyPrefixMatch.Groups[1].Value + text;
            }
            else
            {
                message.Content = text;
            }
            message.IsEdited = true;
            message.EditedAt = now;

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == message.ConversationId, cancellationToken);

            if (conversation is not null)
            {
                conversation.SetUpdatedAt(now);
            }

            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                await _signalR.BroadcastMessageEditedAsync(
                    message.ConversationId,
                    message.Id,
                    message.Content,
                    now,
                    cancellationToken);
            }
            catch
            {
                // Best-effort SignalR broadcast
            }

            return Result.Success(new EditChatMessageResult(
                message.Id,
                message.ConversationId,
                message.Content,
                true,
                now));
        }

        // 2. Try legacy ChatMessages table
        var legacy = await _context.ChatMessages
            .FirstOrDefaultAsync(cm => cm.Id == request.MessageId, cancellationToken);

        if (legacy is not null)
        {
            if (legacy.IsDeletedForEveryone)
                return Result.Failure<EditChatMessageResult>("Cannot edit a deleted message.", "MESSAGE_DELETED");

            if (legacy.SenderAccountId != callerAccountId)
                return Result.Failure<EditChatMessageResult>("You can only edit your own messages.", "FORBIDDEN");

            legacy.Message = text;
            legacy.IsEdited = true;
            legacy.EditedAt = now;

            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                await _signalR.BroadcastMessageEditedAsync(
                    legacy.OrderRequestId,
                    legacy.Id,
                    legacy.Message,
                    now,
                    cancellationToken);
            }
            catch
            {
                // Best-effort SignalR broadcast
            }

            return Result.Success(new EditChatMessageResult(
                legacy.Id,
                legacy.OrderRequestId,
                legacy.Message,
                true,
                now));
        }

        return Result.Failure<EditChatMessageResult>("Message not found.", "MESSAGE_NOT_FOUND");
    }
}
