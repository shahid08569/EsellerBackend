using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Chat.DeleteChatMessage;

public class BatchDeleteChatMessagesCommandHandler
    : IRequestHandler<BatchDeleteChatMessagesCommand, Result<BatchDeleteChatMessagesResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly ISignalRNotificationService _signalR;

    public BatchDeleteChatMessagesCommandHandler(
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

    public async Task<Result<BatchDeleteChatMessagesResult>> Handle(
        BatchDeleteChatMessagesCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<BatchDeleteChatMessagesResult>("User is not authenticated.", "UNAUTHENTICATED");

        if (request.MessageIds is null || request.MessageIds.Count == 0)
            return Result.Failure<BatchDeleteChatMessagesResult>("No messages selected.", "VALIDATION_ERROR");

        var callerAccountId = _currentUserService.AccountId.Value;
        var callerIdStr = callerAccountId.ToString();
        var now = _dateTime.UtcNow;
        var distinctIds = request.MessageIds.Distinct().ToList();

        // 1. Fetch matching modern messages
        var modernMessages = await _context.Messages
            .Where(m => distinctIds.Contains(m.Id))
            .ToListAsync(cancellationToken);

        // 2. Fetch matching legacy chat messages
        var legacyMessages = await _context.ChatMessages
            .Where(cm => distinctIds.Contains(cm.Id))
            .ToListAsync(cancellationToken);

        if (modernMessages.Count == 0 && legacyMessages.Count == 0)
            return Result.Failure<BatchDeleteChatMessagesResult>("No matching messages found.", "NOT_FOUND");

        // If deleting for everyone: enforce that ALL messages belong to the caller!
        if (request.ForEveryone)
        {
            var hasIncomingModern = modernMessages.Any(m => m.SenderId != callerAccountId);
            var hasIncomingLegacy = legacyMessages.Any(cm => cm.SenderAccountId != callerAccountId);

            if (hasIncomingModern || hasIncomingLegacy)
            {
                return Result.Failure<BatchDeleteChatMessagesResult>(
                    "Delete for everyone is only available when all selected messages were sent by you.",
                    "FORBIDDEN");
            }

            foreach (var m in modernMessages)
            {
                m.IsDeletedForEveryone = true;
                m.DeletedForEveryoneAt = now;
                m.Content = "This message was deleted";
                m.AttachmentUrl = null;
                m.AttachmentFileName = null;
                m.AttachmentContentType = null;
                m.AttachmentSizeBytes = null;
            }

            foreach (var cm in legacyMessages)
            {
                cm.IsDeletedForEveryone = true;
                cm.DeletedForEveryoneAt = now;
                cm.Message = "This message was deleted";
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Broadcast deletions via SignalR
            foreach (var m in modernMessages)
            {
                try
                {
                    await _signalR.BroadcastMessageDeletedAsync(m.ConversationId, m.Id, true, cancellationToken);
                }
                catch { }
            }

            foreach (var cm in legacyMessages)
            {
                try
                {
                    await _signalR.BroadcastMessageDeletedAsync(cm.OrderRequestId, cm.Id, true, cancellationToken);
                }
                catch { }
            }

            return Result.Success(new BatchDeleteChatMessagesResult(
                modernMessages.Count + legacyMessages.Count,
                true));
        }
        else
        {
            // Delete for me
            foreach (var m in modernMessages)
            {
                var list = (m.DeletedForAccountIds ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                if (!list.Contains(callerIdStr, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(callerIdStr);
                    m.DeletedForAccountIds = string.Join(",", list);
                }
            }

            foreach (var cm in legacyMessages)
            {
                var list = (cm.DeletedForAccountIds ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                if (!list.Contains(callerIdStr, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(callerIdStr);
                    cm.DeletedForAccountIds = string.Join(",", list);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success(new BatchDeleteChatMessagesResult(
                modernMessages.Count + legacyMessages.Count,
                false));
        }
    }
}
