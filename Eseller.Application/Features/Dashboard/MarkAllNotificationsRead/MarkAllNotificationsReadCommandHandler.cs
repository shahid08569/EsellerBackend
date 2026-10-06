using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Dashboard.MarkAllNotificationsRead;

public class MarkAllNotificationsReadCommandHandler
    : IRequestHandler<MarkAllNotificationsReadCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly ISignalRNotificationService _signalR;

    public MarkAllNotificationsReadCommandHandler(
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
        MarkAllNotificationsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var accountId = _currentUserService.AccountId.Value;
        var notifications = await _context.Notifications
            .Where(n => n.RecipientUserId == accountId && !n.IsRead)
            .ToListAsync(cancellationToken);

        var now = _dateTime.UtcNow;
        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
            notification.SetUpdatedAt(now);
        }

        if (notifications.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _signalR.SendUnreadCountChangedAsync(accountId, 0, cancellationToken);
        }
        catch { /* best-effort */ }

        return Result.Success();
    }
}
