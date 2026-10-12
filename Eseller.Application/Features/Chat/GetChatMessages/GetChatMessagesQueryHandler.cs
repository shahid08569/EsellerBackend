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
    private readonly IDateTime _dateTime;

    public GetChatMessagesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<PagedList<ChatMessageDto>>> Handle(
        GetChatMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<ChatMessageDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == request.OrderRequestId
                                   || c.ParticipantId == request.OrderRequestId, cancellationToken);

        if (conversation is null)
        {
            // Lazy ensure shop/user rooms
            var isShop = await _context.Shops.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(s => s.Id == request.OrderRequestId && s.DeletedAt == null, cancellationToken);
            if (isShop)
            {
                conversation = await SupportConversationService.GetOrCreateAsync(
                    _context, request.OrderRequestId, ChatParticipantType.Seller,
                    _dateTime.UtcNow, cancellationToken);
            }
            else
            {
                var isUser = await _context.Users.AsNoTracking()
                    .AnyAsync(u => u.Id == request.OrderRequestId, cancellationToken);
                if (isUser)
                {
                    conversation = await SupportConversationService.GetOrCreateAsync(
                        _context, request.OrderRequestId, ChatParticipantType.Customer,
                        _dateTime.UtcNow, cancellationToken);
                }
                else
                {
                    var order = await _context.OrderRequests.AsNoTracking()
                        .FirstOrDefaultAsync(o => o.Id == request.OrderRequestId, cancellationToken);
                    if (order is not null && order.UserId != Guid.Empty)
                    {
                        conversation = await SupportConversationService.GetOrCreateAsync(
                            _context, order.UserId, ChatParticipantType.Customer,
                            _dateTime.UtcNow, cancellationToken);
                    }
                }
            }
        }

        if (conversation is null)
            return Result.Failure<PagedList<ChatMessageDto>>(
                "Conversation not found.", "ORDER_NOT_FOUND");

        if (!await SupportConversationService.CanAccessAsync(
                _context, _currentUserService, conversation, cancellationToken))
            return Result.Failure<PagedList<ChatMessageDto>>(
                "You do not have permission.", "FORBIDDEN");

        var roomIds = new[] { conversation.Id, conversation.ParticipantId, request.OrderRequestId }
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var accountIdStr = _currentUserService.AccountId.Value.ToString();

        // 1. Modern Messages table
        var modernQuery = _context.Messages
            .AsNoTracking()
            .Where(m => roomIds.Contains(m.ConversationId)
                && (m.DeletedForAccountIds == null || !m.DeletedForAccountIds.Contains(accountIdStr)));

        var modernCount = await modernQuery.CountAsync(cancellationToken);

        List<ChatMessageDto> messages;
        int totalCount;

        if (modernCount > 0)
        {
            totalCount = modernCount;
            messages = await modernQuery
                .OrderByDescending(m => m.SentAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(m => new ChatMessageDto(
                    m.Id,
                    m.ConversationId,
                    m.SenderId,
                    m.SenderRole.ToString(),
                    m.IsDeletedForEveryone ? "This message was deleted" : m.Content,
                    m.IsRead,
                    m.ReadAt,
                    null,
                    m.SentAt,
                    m.IsDeletedForEveryone ? null : m.AttachmentUrl,
                    m.IsDeletedForEveryone ? null : m.AttachmentFileName,
                    m.IsDeletedForEveryone ? null : m.AttachmentContentType,
                    m.IsDeletedForEveryone ? null : m.AttachmentSizeBytes,
                    m.IsEdited,
                    m.EditedAt,
                    m.IsDeletedForEveryone))
                .ToListAsync(cancellationToken);
        }
        else
        {
            // 2. Legacy ChatMessages table
            var legacyQuery = _context.ChatMessages
                .AsNoTracking()
                .Where(cm => roomIds.Contains(cm.OrderRequestId)
                    && (cm.DeletedForAccountIds == null || !cm.DeletedForAccountIds.Contains(accountIdStr)));

            totalCount = await legacyQuery.CountAsync(cancellationToken);

            messages = await legacyQuery
                .OrderByDescending(cm => cm.SentAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(cm => new ChatMessageDto(
                    cm.Id,
                    cm.OrderRequestId,
                    cm.SenderAccountId,
                    cm.SenderRole.ToString(),
                    cm.IsDeletedForEveryone ? "This message was deleted" : cm.Message,
                    cm.IsRead,
                    cm.ReadAt,
                    cm.DeliveredAt,
                    cm.SentAt,
                    null,
                    null,
                    null,
                    null,
                    cm.IsEdited,
                    cm.EditedAt,
                    cm.IsDeletedForEveryone))
                .ToListAsync(cancellationToken);
        }

        // Oldest → newest for chat feed display
        messages.Reverse();

        return Result.Success(new PagedList<ChatMessageDto>(
            messages, totalCount, request.PageNumber, request.PageSize));
    }
}
