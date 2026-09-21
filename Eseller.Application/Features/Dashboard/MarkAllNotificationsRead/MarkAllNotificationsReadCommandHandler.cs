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

    public MarkAllNotificationsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(
        MarkAllNotificationsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var notifications = await _context.Notifications
            .Where(n => n.AccountId == _currentUserService.AccountId.Value && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
            notification.IsRead = true;

        if (notifications.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}