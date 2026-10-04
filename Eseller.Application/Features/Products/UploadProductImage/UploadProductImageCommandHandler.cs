using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.UploadProductImage;

public class UploadProductImageCommandHandler
    : IRequestHandler<UploadProductImageCommand, Result<Guid>>
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private const int MaxImagesPerProduct = 10;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;

    public UploadProductImageCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Guid>> Handle(
        UploadProductImageCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null, cancellationToken);

        if (product is null)
            return Result.Failure<Guid>("Product not found.", "PRODUCT_NOT_FOUND");

        // Permission check
        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        if (!isAdmin)
        {
            var shop = await _context.Shops
                .FirstOrDefaultAsync(s => s.Id == product.ShopId, cancellationToken);

            var shopkeeper = shop is null ? null : await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

            if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                return Result.Failure<Guid>("You do not have permission.", "FORBIDDEN");
        }

        // Validate file
        if (request.FileSize > MaxFileSize)
            return Result.Failure<Guid>("File size exceeds 5 MB limit.", "FILE_TOO_LARGE");

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return Result.Failure<Guid>(
                "Invalid file type. Allowed: jpg, jpeg, png, webp.",
                "INVALID_FILE_TYPE");

        // Max images check
        var existingCount = await _context.ProductImages
            .CountAsync(pi => pi.ProductId == request.ProductId, cancellationToken);

        if (existingCount >= MaxImagesPerProduct)
            return Result.Failure<Guid>(
                $"Maximum {MaxImagesPerProduct} images allowed per product.",
                "MAX_IMAGES_EXCEEDED");

        // Upload file
        var uniqueFileName = $"products/{product.Id}/image-{Guid.NewGuid()}{extension}";
        var imageUrl = await _fileStorageService.UploadAsync(
            request.FileStream,
            uniqueFileName,
            request.ContentType,
            cancellationToken);

        // Get next sort order; first image becomes cover
        var maxSortOrder = await _context.ProductImages
            .Where(pi => pi.ProductId == request.ProductId)
            .MaxAsync(pi => (int?)pi.SortOrder, cancellationToken) ?? 0;

        var isFirstImage = existingCount == 0;

        // Create ProductImage
        var productImage = new ProductImage
        {
            ProductId = request.ProductId,
            ImageUrl = imageUrl,
            SortOrder = isFirstImage ? 0 : maxSortOrder + 1,
            IsCover = isFirstImage
        };

        _context.ProductImages.Add(productImage);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(productImage.Id);
    }
}