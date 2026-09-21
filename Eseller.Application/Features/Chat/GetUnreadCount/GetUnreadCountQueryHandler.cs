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

        var role = _currentUserService.RoleType;

        if (role == RoleType.SuperAdmin || role == RoleType.Partner)
        {
            // Admin: unread messages from users
            var count = await _context.ChatMessages
                .CountAsync(cm => !cm.IsRead && cm.SenderRole == ChatSenderRole.User,
                    cancellationToken);

            return Result.Success(count);
        }
        else if (role == RoleType.User)
        {
            // Customer: unread messages from SuperAdmin/Partner
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                    cancellationToken);

            if (user is null)
                return Result.Success(0);

            var orderIds = await _context.OrderRequests
                .Where(o => o.UserId == user.Id)
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);

            var count = await _context.ChatMessages
                .CountAsync(cm => orderIds.Contains(cm.OrderRequestId)
                               && !cm.IsRead
                               && cm.SenderRole != ChatSenderRole.User,
                    cancellationToken);

            return Result.Success(count);
        }

        return Result.Success(0);
    }
}