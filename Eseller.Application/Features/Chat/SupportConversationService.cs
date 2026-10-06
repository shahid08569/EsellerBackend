using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Chat;
using Eseller.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Chat;

/// <summary>
/// Get-or-create support conversations and authorization against JWT identity.
/// </summary>
public static class SupportConversationService
{
    public static async Task<Conversation> GetOrCreateAsync(
        IApplicationDbContext context,
        Guid participantId,
        ChatParticipantType participantType,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var existing = await context.Conversations
            .FirstOrDefaultAsync(
                c => c.ParticipantId == participantId && c.ParticipantType == participantType,
                cancellationToken);

        if (existing is not null)
            return existing;

        // Preserve legacy room keys so Angular/SignalR keep working after migration:
        // Seller → Conversation.Id = Shop.Id; Customer → Conversation.Id = User.Id
        var conversation = new Conversation
        {
            Id = participantId,
            ParticipantId = participantId,
            ParticipantType = participantType,
            LastMessageAt = null
        };
        conversation.SetCreatedAt(utcNow);
        conversation.SetUpdatedAt(utcNow);

        context.Conversations.Add(conversation);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return conversation;
        }
        catch (DbUpdateException)
        {
            // Race: another request created the unique row
            context.Conversations.Remove(conversation);
            var raced = await context.Conversations
                .FirstAsync(
                    c => c.ParticipantId == participantId && c.ParticipantType == participantType,
                    cancellationToken);
            return raced;
        }
    }

    public static async Task<bool> CanAccessAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        Conversation conversation,
        CancellationToken cancellationToken)
    {
        if (currentUser.AccountId is null)
            return false;

        var role = currentUser.RoleType;
        if (role == RoleType.SuperAdmin || role == RoleType.Partner)
            return true;

        if (role == RoleType.User && conversation.ParticipantType == ChatParticipantType.Customer)
        {
            var userId = await context.Users.AsNoTracking()
                .Where(u => u.AccountId == currentUser.AccountId.Value)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync(cancellationToken);
            return userId.HasValue && userId.Value == conversation.ParticipantId;
        }

        if (role == RoleType.Shopkeeper && conversation.ParticipantType == ChatParticipantType.Seller)
        {
            var shopkeeper = await context.Shopkeepers.AsNoTracking()
                .FirstOrDefaultAsync(sk => sk.AccountId == currentUser.AccountId.Value, cancellationToken);
            if (shopkeeper is null)
                return false;

            return await context.Shops.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(
                    s => s.Id == conversation.ParticipantId
                         && s.ShopkeeperId == shopkeeper.Id
                         && s.DeletedAt == null,
                    cancellationToken);
        }

        return false;
    }

    public static ChatSenderRole MapSenderRole(RoleType role) => role switch
    {
        RoleType.SuperAdmin => ChatSenderRole.SuperAdmin,
        RoleType.Partner => ChatSenderRole.Partner,
        RoleType.Shopkeeper => ChatSenderRole.Shopkeeper,
        RoleType.User => ChatSenderRole.User,
        _ => ChatSenderRole.User
    };

    public static bool IsAdminRole(ChatSenderRole role) =>
        role is ChatSenderRole.SuperAdmin or ChatSenderRole.Partner;

    public static bool IsInboundForAdmin(ChatSenderRole role) =>
        role is ChatSenderRole.User or ChatSenderRole.Shopkeeper;
}
