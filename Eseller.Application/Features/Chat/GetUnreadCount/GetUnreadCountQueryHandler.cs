using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Chat.GetUnreadCount;

public class GetUnreadCountQueryHandler
    : IRequestHandler<GetUnreadCountQuery, Result<int>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetUnreadCountQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<int>> Handle(
        GetUnreadCountQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<int>("User is not authenticated.", "UNAUTHENTICATED");

        try
        {
            return Result.Success(await CountUnreadAsync(cancellationToken));
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Success(0);
        }
        catch (OperationCanceledException)
        {
            return Result.Success(0);
        }
    }

    private async Task<int> CountUnreadAsync(CancellationToken cancellationToken)
    {
        var role = _currentUserService.RoleType;

        if (role == RoleType.SuperAdmin || role == RoleType.Partner)
        {
            return await _context.Messages.AsNoTracking()
                .CountAsync(m => !m.IsRead
                                 && (m.SenderRole == ChatSenderRole.Shopkeeper
                                     || m.SenderRole == ChatSenderRole.User),
                    cancellationToken);
        }

        if (role == RoleType.Shopkeeper)
        {
            var shopkeeper = await _context.Shopkeepers.AsNoTracking()
                .FirstOrDefaultAsync(sk => sk.AccountId == _currentUserService.AccountId!.Value,
                    cancellationToken);
            if (shopkeeper is null) return 0;

            var shopIds = await _context.Shops.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            if (shopIds.Count == 0) return 0;

            var convoIds = await _context.Conversations.AsNoTracking()
                .Where(c => c.ParticipantType == ChatParticipantType.Seller
                            && shopIds.Contains(c.ParticipantId))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            if (convoIds.Count == 0) return 0;

            return await _context.Messages.AsNoTracking()
                .CountAsync(m => convoIds.Contains(m.ConversationId)
                                 && !m.IsRead
                                 && (m.SenderRole == ChatSenderRole.SuperAdmin
                                     || m.SenderRole == ChatSenderRole.Partner),
                    cancellationToken);
        }

        if (role == RoleType.User)
        {
            var userId = await _context.Users.AsNoTracking()
                .Where(u => u.AccountId == _currentUserService.AccountId!.Value)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (userId is null) return 0;

            var convoId = await _context.Conversations.AsNoTracking()
                .Where(c => c.ParticipantType == ChatParticipantType.Customer
                            && c.ParticipantId == userId.Value)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (convoId is null) return 0;

            return await _context.Messages.AsNoTracking()
                .CountAsync(m => m.ConversationId == convoId.Value
                                 && !m.IsRead
                                 && m.SenderRole != ChatSenderRole.User,
                    cancellationToken);
        }

        return 0;
    }
}
