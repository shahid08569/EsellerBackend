using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Cart.ClearCart;

public class ClearCartCommandHandler
    : IRequestHandler<ClearCartCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ClearCartCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(
        ClearCartCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User)
            return Result.Failure("Only customers can modify cart.", "FORBIDDEN");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure("User not found.", "USER_NOT_FOUND");

        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);

        if (cart is null)
            return Result.Success();   // Already empty — idempotent

        var items = await _context.CartItems
            .Where(ci => ci.CartId == cart.Id)
            .ToListAsync(cancellationToken);

        if (items.Count > 0)
        {
            _context.CartItems.RemoveRange(items);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}