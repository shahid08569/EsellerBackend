using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Chat.SendChatMessage;

/// <param name="ConversationId">Support conversation id (Seller = Shop.Id, Customer = User.Id).</param>
public record SendChatMessageCommand(
    Guid ConversationId,
    string? Message,
    string? AttachmentUrl = null,
    string? AttachmentFileName = null,
    string? AttachmentContentType = null,
    long? AttachmentSizeBytes = null) : IRequest<Result<Guid>>;
