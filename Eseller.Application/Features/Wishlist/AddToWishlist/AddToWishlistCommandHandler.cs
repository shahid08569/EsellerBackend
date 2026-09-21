using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Engagement;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Wishlist.AddToWishlist;

public class AddToWishlistCommandHandler
    : IRequestHandler<AddToWishlistCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public AddToWishlistCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        AddToWishlistCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User)
            return Result.Failure<Guid>("Only customers can use wishlist.", "FORBIDDEN");

        // 2. Get user
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<Guid>("User not found.", "USER_NOT_FOUND");

        // 3. Verify product
        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId
                                   && p.IsActive
                                   && p.IsApproved
                                   && p.DeletedAt == null,
                cancellationToken);

        if (product is null)
            return Result.Failure<Guid>("Product not available.", "PRODUCT_NOT_AVAILABLE");

        // 4. Check duplicate
        var existing = await _context.Wishlists
            .FirstOrDefaultAsync(w => w.UserId == user.Id && w.ProductId == request.ProductId,
                cancellationToken);

        if (existing is not null)
            return Result.Failure<Guid>("Product is already in wishlist.", "ALREADY_IN_WISHLIST");

        // 5. Add to wishlist
        var wishlistItem = new Domain.Entities.Engagement.Wishlist
        {
            UserId = user.Id,
            ProductId = request.ProductId,
            CreatedAt = _dateTime.UtcNow
        };

        _context.Wishlists.Add(wishlistItem);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(wishlistItem.Id);
    }
}