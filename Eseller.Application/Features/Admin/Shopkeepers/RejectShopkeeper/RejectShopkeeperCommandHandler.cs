using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Admin.Shopkeepers.RejectShopkeeper;

public class RejectShopkeeperCommandHandler
    : IRequestHandler<RejectShopkeeperCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public RejectShopkeeperCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        RejectShopkeeperCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(s => s.Id == request.ShopkeeperId, cancellationToken);

        if (shopkeeper is null)
            return Result.Failure("Shopkeeper not found.", "SHOPKEEPER_NOT_FOUND");

        if (_currentUserService.RoleType == RoleType.Partner)
        {
            var partner = await _context.Partners.AsNoTracking()
                .FirstOrDefaultAsync(p => p.AccountId == _currentUserService.AccountId.Value, cancellationToken);
            if (partner == null || shopkeeper.PartnerId != partner.Id)
            {
                return Result.Failure("Partners can only reject merchants that registered through their own referral link.", "FORBIDDEN");
            }
        }

        if (shopkeeper.Status != ShopkeeperApprovalStatus.Pending)
            return Result.Failure(
                $"Shopkeeper is already {shopkeeper.Status}.",
                "INVALID_STATUS");

        shopkeeper.Status = ShopkeeperApprovalStatus.Rejected;
        shopkeeper.ApprovedByAccountId = _currentUserService.AccountId;
        shopkeeper.ApprovedAt = _dateTime.UtcNow;
        shopkeeper.RejectionReason = request.Reason;
        shopkeeper.SetUpdatedAt(_dateTime.UtcNow);

        _context.AuditLogs.Add(new Eseller.Domain.Entities.Identity.AuditLog
        {
            Id = Guid.NewGuid(),
            ActorAccountId = _currentUserService.AccountId,
            ActorType = _currentUserService.RoleType.ToString(),
            Action = "REJECT_SHOPKEEPER",
            EntityType = "Shopkeeper",
            EntityId = shopkeeper.Id,
            OldValues = "Status: Pending",
            NewValues = $"Status: Rejected | Reason: {request.Reason}",
            IpAddress = null,
            CreatedAt = _dateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
