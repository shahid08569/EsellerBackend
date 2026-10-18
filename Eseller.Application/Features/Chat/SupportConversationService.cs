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
        // SellerPartner & Partner → unique Conversation.Id
        var conversation = new Conversation
        {
            Id = (participantType == ChatParticipantType.Seller || participantType == ChatParticipantType.Customer)
                ? participantId
                : Guid.NewGuid(),
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
        if (role == RoleType.SuperAdmin)
            return true;

        if (role == RoleType.Partner)
        {
            var partner = await context.Partners.AsNoTracking()
                .FirstOrDefaultAsync(p => p.AccountId == currentUser.AccountId.Value, cancellationToken);
            if (partner == null)
                return false;

            // Partner direct channel with SuperAdmin
            if (conversation.ParticipantType == ChatParticipantType.Partner)
            {
                return conversation.ParticipantId == partner.Id;
            }

            // Partner can ONLY access SellerPartner chat if the shop registered via this partner's referral code
            if (conversation.ParticipantType == ChatParticipantType.SellerPartner)
            {
                return await context.Shops.IgnoreQueryFilters().AsNoTracking()
                    .AnyAsync(s => (s.Id == conversation.ParticipantId || s.Id == conversation.Id)
                                && s.PartnerId == partner.Id
                                && s.DeletedAt == null, cancellationToken);
            }

            // Partner strictly forbidden from customer chats and normal seller-superadmin chats
            return false;
        }

        if (role == RoleType.User && conversation.ParticipantType == ChatParticipantType.Customer)
        {
            var userId = await context.Users.AsNoTracking()
                .Where(u => u.AccountId == currentUser.AccountId.Value)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync(cancellationToken);
            return userId.HasValue && userId.Value == conversation.ParticipantId;
        }

        if (role == RoleType.Shopkeeper)
        {
            if (conversation.ParticipantType != ChatParticipantType.Seller &&
                conversation.ParticipantType != ChatParticipantType.SellerPartner)
                return false;

            var shopkeeper = await context.Shopkeepers.AsNoTracking()
                .FirstOrDefaultAsync(sk => sk.AccountId == currentUser.AccountId.Value, cancellationToken);
            if (shopkeeper is null)
                return false;

            var shop = await context.Shops.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(
                    s => (s.Id == conversation.ParticipantId || s.Id == conversation.Id)
                         && s.ShopkeeperId == shopkeeper.Id
                         && s.DeletedAt == null,
                    cancellationToken);

            if (shop is null)
                return false;

            // If SellerPartner, shop must have this partner referral
            if (conversation.ParticipantType == ChatParticipantType.SellerPartner)
            {
                return shop.PartnerId != null;
            }

            return true;
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
