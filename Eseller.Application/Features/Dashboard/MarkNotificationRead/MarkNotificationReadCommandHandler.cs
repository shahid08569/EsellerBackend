using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Dashboard.MarkNotificationRead;

public class MarkNotificationReadCommandHandler
    : IRequestHandler<MarkNotificationReadCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly ISignalRNotificationService _signalR;

    public MarkNotificationReadCommandHandler(
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

    public async Task<Result> Handle(
        MarkNotificationReadCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var accountId = _currentUserService.AccountId.Value;
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId
                                   && n.RecipientUserId == accountId,
                cancellationToken);

        if (notification is null)
            return Result.Failure("Notification not found.", "NOTIFICATION_NOT_FOUND");

        if (!notification.IsRead)
        {
            var now = _dateTime.UtcNow;
            notification.IsRead = true;
            notification.ReadAt = now;
            notification.SetUpdatedAt(now);
            await _context.SaveChangesAsync(cancellationToken);

            var unread = await _context.Notifications.AsNoTracking()
                .CountAsync(n => n.RecipientUserId == accountId && !n.IsRead, cancellationToken);
            try
            {
                await _signalR.SendUnreadCountChangedAsync(accountId, unread, cancellationToken);
            }
            catch { /* best-effort */ }
        }

        return Result.Success();
    }
}
