using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.DeleteAllProductImages;

public class DeleteAllProductImagesCommandHandler
    : IRequestHandler<DeleteAllProductImagesCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;

    public DeleteAllProductImagesCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(
        DeleteAllProductImagesCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null, cancellationToken);

        if (product is null)
            return Result.Failure("Product not found.", "PRODUCT_NOT_FOUND");

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        if (!isAdmin)
        {
            var shop = await _context.Shops
                .FirstOrDefaultAsync(s => s.Id == product.ShopId, cancellationToken);

            var shopkeeper = shop is null ? null : await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

            if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                return Result.Failure("You do not have permission.", "FORBIDDEN");
        }

        var images = await _context.ProductImages
            .Where(pi => pi.ProductId == request.ProductId)
            .ToListAsync(cancellationToken);

        // Delete all files
        foreach (var image in images)
        {
            await _fileStorageService.DeleteAsync(image.ImageUrl, cancellationToken);
        }

        // Remove all from DB
        _context.ProductImages.RemoveRange(images);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}