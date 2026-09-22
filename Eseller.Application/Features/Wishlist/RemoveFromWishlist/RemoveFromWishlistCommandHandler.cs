using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Wishlist.RemoveFromWishlist;

public class RemoveFromWishlistCommandHandler
    : IRequestHandler<RemoveFromWishlistCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public RemoveFromWishlistCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(
        RemoveFromWishlistCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User)
            return Result.Failure("Only customers can modify wishlist.", "FORBIDDEN");

        // 2. Get user
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure("User not found.", "USER_NOT_FOUND");

        // 3. Find wishlist item
        var item = await _context.Wishlists
            .FirstOrDefaultAsync(w => w.UserId == user.Id && w.ProductId == request.ProductId,
                cancellationToken);

        if (item is null)
            return Result.Failure("Item not found in wishlist.", "NOT_IN_WISHLIST");

        // 4. Remove
        _context.Wishlists.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}