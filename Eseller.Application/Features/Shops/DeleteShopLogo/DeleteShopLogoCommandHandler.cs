using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Shops.DeleteShopLogo;

public class DeleteShopLogoCommandHandler
    : IRequestHandler<DeleteShopLogoCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public DeleteShopLogoCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        DeleteShopLogoCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == request.ShopId, cancellationToken);

        if (shop is null)
            return Result.Failure("Shop not found.", "SHOP_NOT_FOUND");

        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

        if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
            return Result.Failure(
                "You do not have permission to update this shop.",
                "FORBIDDEN");

        if (!string.IsNullOrEmpty(shop.LogoUrl))
        {
            await _fileStorageService.DeleteAsync(shop.LogoUrl, cancellationToken);
            shop.LogoUrl = null;
            shop.SetUpdatedAt(_dateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}