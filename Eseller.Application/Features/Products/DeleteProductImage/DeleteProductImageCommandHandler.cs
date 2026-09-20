using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.DeleteProductImage;

public class DeleteProductImageCommandHandler
    : IRequestHandler<DeleteProductImageCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;

    public DeleteProductImageCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(
        DeleteProductImageCommand request,
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

        var image = await _context.ProductImages
            .FirstOrDefaultAsync(pi => pi.Id == request.ImageId && pi.ProductId == request.ProductId,
                cancellationToken);

        if (image is null)
            return Result.Failure("Product image not found.", "IMAGE_NOT_FOUND");

        // Delete file from storage
        await _fileStorageService.DeleteAsync(image.ImageUrl, cancellationToken);

        // Remove from DB
        _context.ProductImages.Remove(image);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}