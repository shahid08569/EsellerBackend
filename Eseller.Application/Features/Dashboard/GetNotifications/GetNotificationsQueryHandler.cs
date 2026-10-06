using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Dashboard.GetNotifications;

public class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, Result<PagedList<NotificationDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetNotificationsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<NotificationDto>>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<NotificationDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        try
        {
            var query = _context.Notifications.AsNoTracking()
                .Where(n => n.RecipientUserId == _currentUserService.AccountId.Value);

            if (request.UnreadOnly == true)
                query = query.Where(n => !n.IsRead);

            query = query.OrderByDescending(n => n.CreatedAt);

            var totalCount = await query.CountAsync(cancellationToken);

            var notifications = await query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(n => new NotificationDto(
                    n.Id,
                    n.Type.ToString(),
                    n.Title,
                    n.Message,
                    n.IsRead,
                    n.CreatedAt,
                    n.NavigationUrl,
                    n.EntityId,
                    n.EntityType.ToString(),
                    n.NavigationUrl,
                    n.EntityId))
                .ToListAsync(cancellationToken);

            var pagedList = new PagedList<NotificationDto>(
                notifications, totalCount, request.PageNumber, request.PageSize);

            return Result.Success(pagedList);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Success(new PagedList<NotificationDto>(
                Array.Empty<NotificationDto>(), 0, request.PageNumber, request.PageSize));
        }
        catch (OperationCanceledException)
        {
            return Result.Success(new PagedList<NotificationDto>(
                Array.Empty<NotificationDto>(), 0, request.PageNumber, request.PageSize));
        }
    }
}
