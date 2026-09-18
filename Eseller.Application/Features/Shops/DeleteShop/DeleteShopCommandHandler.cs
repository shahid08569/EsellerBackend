using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Shops.DeleteShop;

public class DeleteShopCommandHandler
    : IRequestHandler<DeleteShopCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public DeleteShopCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        DeleteShopCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        // 2. Find shop
        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == request.ShopId, cancellationToken);

        if (shop is null)
            return Result.Failure("Shop not found.", "SHOP_NOT_FOUND");

        // 3. Data Isolation — verify ownership
        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

        if (shopkeeper is null)
            return Result.Failure("Shopkeeper not found.", "SHOPKEEPER_NOT_FOUND");

        if (shopkeeper.AccountId != _currentUserService.AccountId.Value)
            return Result.Failure(
                "You do not have permission to delete this shop.",
                "FORBIDDEN");

        // 4. Soft delete
        shop.IsActive = false;
        shop.DeletedAt = _dateTime.UtcNow;
        shop.SetUpdatedAt(_dateTime.UtcNow);

        // 5. Save
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}