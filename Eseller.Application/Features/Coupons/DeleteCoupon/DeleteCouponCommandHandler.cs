using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Coupons.DeleteCoupon;

public class DeleteCouponCommandHandler
    : IRequestHandler<DeleteCouponCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public DeleteCouponCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        DeleteCouponCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.Id == request.CouponId, cancellationToken);

        if (coupon is null)
            return Result.Failure("Coupon not found.", "COUPON_NOT_FOUND");

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        if (!isAdmin && coupon.CreatedByAccountId != _currentUserService.AccountId.Value)
            return Result.Failure("You do not have permission.", "FORBIDDEN");

        coupon.IsActive = false;
        coupon.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}