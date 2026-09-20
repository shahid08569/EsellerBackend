using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ProductVariants.UploadVariantImage;

public class UploadVariantImageCommandHandler
    : IRequestHandler<UploadVariantImageCommand, Result<string>>
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public UploadVariantImageCommandHandler(
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

    public async Task<Result<string>> Handle(
        UploadVariantImageCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<string>("User is not authenticated.", "UNAUTHENTICATED");

        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null, cancellationToken);

        if (product is null)
            return Result.Failure<string>("Product not found.", "PRODUCT_NOT_FOUND");

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
                return Result.Failure<string>("You do not have permission.", "FORBIDDEN");
        }

        var variant = await _context.ProductVariants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pv => pv.Id == request.VariantId
                                    && pv.ProductId == request.ProductId
                                    && pv.DeletedAt == null,
                cancellationToken);

        if (variant is null)
            return Result.Failure<string>("Variant not found.", "VARIANT_NOT_FOUND");

        if (request.FileSize > MaxFileSize)
            return Result.Failure<string>("File size exceeds 5 MB limit.", "FILE_TOO_LARGE");

        var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return Result.Failure<string>(
                "Invalid file type. Allowed: jpg, jpeg, png, webp.",
                "INVALID_FILE_TYPE");

        // Delete old image
        if (!string.IsNullOrEmpty(variant.ImageUrl))
        {
            await _fileStorageService.DeleteAsync(variant.ImageUrl, cancellationToken);
        }

        // Upload new image
        var uniqueFileName = $"products/{product.Id}/variants/{variant.Id}/image-{Guid.NewGuid()}{ext}";
        var imageUrl = await _fileStorageService.UploadAsync(
            request.FileStream,
            uniqueFileName,
            request.ContentType,
            cancellationToken);

        variant.ImageUrl = imageUrl;
        variant.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(imageUrl);
    }
}