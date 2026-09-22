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

    public MarkNotificationReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(
        MarkNotificationReadCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId
                                   && n.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (notification is null)
            return Result.Failure("Notification not found.", "NOTIFICATION_NOT_FOUND");

        notification.IsRead = true;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}