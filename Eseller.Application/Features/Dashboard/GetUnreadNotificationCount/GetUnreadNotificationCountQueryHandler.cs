using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Dashboard.GetUnreadNotificationCount;

public class GetUnreadNotificationCountQueryHandler
    : IRequestHandler<GetUnreadNotificationCountQuery, Result<int>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetUnreadNotificationCountQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<int>> Handle(
        GetUnreadNotificationCountQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<int>("User is not authenticated.", "UNAUTHENTICATED");

        try
        {
            var count = await _context.Notifications.AsNoTracking()
                .CountAsync(n => n.RecipientUserId == _currentUserService.AccountId.Value && !n.IsRead,
                    cancellationToken);

            return Result.Success(count);
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
}
