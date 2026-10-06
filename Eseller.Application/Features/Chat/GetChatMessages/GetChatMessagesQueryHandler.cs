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
            .FirstOrDefaultAsync(c => c.Id == request.OrderRequestId, cancellationToken);

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
            }
        }

        if (conversation is null)
            return Result.Failure<PagedList<ChatMessageDto>>(
                "Conversation not found.", "ORDER_NOT_FOUND");

        if (!await SupportConversationService.CanAccessAsync(
                _context, _currentUserService, conversation, cancellationToken))
            return Result.Failure<PagedList<ChatMessageDto>>(
                "You do not have permission.", "FORBIDDEN");

        // Latest page first for UX: return chronological page of latest N
        var totalCount = await _context.Messages
            .CountAsync(m => m.ConversationId == conversation.Id, cancellationToken);

        List<ChatMessageDto> messages;

        if (totalCount > 0)
        {
            messages = await _context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversation.Id)
                .OrderByDescending(m => m.SentAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(m => new ChatMessageDto(
                    m.Id,
                    m.ConversationId,
                    m.SenderId,
                    m.SenderRole.ToString(),
                    m.Content,
                    m.IsRead,
                    m.ReadAt,
                    null,
                    m.SentAt,
                    m.AttachmentUrl,
                    m.AttachmentFileName,
                    m.AttachmentContentType,
                    m.AttachmentSizeBytes))
                .ToListAsync(cancellationToken);
        }
        else
        {
            // Legacy ChatMessages (shop.id / order id room keys) before Messages migration
            var legacyRoomIds = new[] { conversation.Id, conversation.ParticipantId }.Distinct().ToList();
            totalCount = await _context.ChatMessages
                .CountAsync(cm => legacyRoomIds.Contains(cm.OrderRequestId), cancellationToken);

            messages = await _context.ChatMessages
                .AsNoTracking()
                .Where(cm => legacyRoomIds.Contains(cm.OrderRequestId))
                .OrderByDescending(cm => cm.SentAt)
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
                    cm.SentAt,
                    null,
                    null,
                    null,
                    null))
                .ToListAsync(cancellationToken);
        }

        // Oldest → newest for chat window
        messages.Reverse();

        return Result.Success(new PagedList<ChatMessageDto>(
            messages, totalCount, request.PageNumber, request.PageSize));
    }
}
