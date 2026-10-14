using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eseller.Application.Features.Products.UploadMultipleProductImages;

public class UploadMultipleProductImagesCommandHandler
    : IRequestHandler<UploadMultipleProductImagesCommand, Result<List<Guid>>>
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private const int MaxImagesPerProduct = 10;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UploadMultipleProductImagesCommandHandler> _logger;

    public UploadMultipleProductImagesCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService,
        ILogger<UploadMultipleProductImagesCommandHandler> logger)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<List<Guid>>> Handle(
        UploadMultipleProductImagesCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<List<Guid>>("User is not authenticated.", "UNAUTHENTICATED");

        if (request.Files.Count == 0)
            return Result.Failure<List<Guid>>("At least one file is required.", "NO_FILES");

        // Find product
        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null, cancellationToken);

        if (product is null)
            return Result.Failure<List<Guid>>("Product not found.", "PRODUCT_NOT_FOUND");

        // Permission check
        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        if (!isAdmin)
        {
            var shop = await _context.Shops
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == product.ShopId, cancellationToken);

            var shopkeeper = shop is null ? null : await _context.Shopkeepers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

            if (shop is null || shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                return Result.Failure<List<Guid>>("You do not have permission.", "FORBIDDEN");
        }

        // Check max limit
        var existingCount = await _context.ProductImages
            .CountAsync(pi => pi.ProductId == request.ProductId, cancellationToken);

        if (existingCount + request.Files.Count > MaxImagesPerProduct)
            return Result.Failure<List<Guid>>(
                $"Maximum {MaxImagesPerProduct} images allowed. You have {existingCount}, adding {request.Files.Count}.",
                "MAX_IMAGES_EXCEEDED");

        // Validate ALL files first (fail-fast)
        foreach (var file in request.Files)
        {
            if (file.FileSize > MaxFileSize)
                return Result.Failure<List<Guid>>(
                    $"File '{file.FileName}' exceeds 5 MB limit.",
                    "FILE_TOO_LARGE");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return Result.Failure<List<Guid>>(
                    $"File '{file.FileName}' has invalid type. Allowed extensions: jpg, jpeg, png, webp, gif.",
                    "INVALID_FILE_TYPE");
        }

        // Get next sort order
        var maxSortOrder = await _context.ProductImages
            .Where(pi => pi.ProductId == request.ProductId)
            .MaxAsync(pi => (int?)pi.SortOrder, cancellationToken) ?? 0;

        var imageIds = new List<Guid>();
        var uploadedUrls = new List<string>();
        var coverAssigned = existingCount > 0
            && await _context.ProductImages.AnyAsync(pi => pi.ProductId == request.ProductId && pi.IsCover, cancellationToken);

        try
        {
            foreach (var file in request.Files)
            {
                if (file.FileStream.CanSeek)
                    file.FileStream.Position = 0;

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var uniqueFileName = $"products/{product.Id}/image-{Guid.NewGuid()}{ext}";

                var imageUrl = await _fileStorageService.UploadAsync(
                    file.FileStream,
                    uniqueFileName,
                    file.ContentType,
                    cancellationToken);

                uploadedUrls.Add(imageUrl);

                maxSortOrder++;
                var makeCover = !coverAssigned;

                var productImage = new ProductImage
                {
                    ProductId = request.ProductId,
                    ImageUrl = imageUrl,
                    SortOrder = makeCover ? 0 : maxSortOrder,
                    IsCover = makeCover
                };

                if (makeCover)
                    coverAssigned = true;

                _context.ProductImages.Add(productImage);
                imageIds.Add(productImage.Id);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success(imageIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload multiple images for product {ProductId}", request.ProductId);

            // Rollback
            foreach (var url in uploadedUrls)
            {
                try { await _fileStorageService.DeleteAsync(url, cancellationToken); }
                catch { }
            }

            return Result.Failure<List<Guid>>($"Upload failed: {ex.Message}", "UPLOAD_FAILED");
        }
    }
}