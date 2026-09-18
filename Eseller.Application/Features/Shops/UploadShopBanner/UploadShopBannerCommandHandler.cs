using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Shops.UploadShopBanner;

public class UploadShopBannerCommandHandler
    : IRequestHandler<UploadShopBannerCommand, Result<string>>
{
    private const long MaxFileSize = 5 * 1024 * 1024;   // 5 MB
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public UploadShopBannerCommandHandler(
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
        UploadShopBannerCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<string>("User is not authenticated.", "UNAUTHENTICATED");

        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == request.ShopId, cancellationToken);

        if (shop is null)
            return Result.Failure<string>("Shop not found.", "SHOP_NOT_FOUND");

        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

        if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
            return Result.Failure<string>(
                "You do not have permission to update this shop.",
                "FORBIDDEN");

        if (request.FileSize > MaxFileSize)
            return Result.Failure<string>(
                "File size exceeds 5 MB limit.",
                "FILE_TOO_LARGE");

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return Result.Failure<string>(
                "Invalid file type. Allowed: jpg, jpeg, png, webp.",
                "INVALID_FILE_TYPE");

        // Delete old banner
        if (!string.IsNullOrEmpty(shop.BannerUrl))
        {
            await _fileStorageService.DeleteAsync(shop.BannerUrl, cancellationToken);
        }

        // Upload new banner
        var uniqueFileName = $"shops/{shop.Id}/banner-{Guid.NewGuid()}{extension}";
        var bannerUrl = await _fileStorageService.UploadAsync(
            request.FileStream,
            uniqueFileName,
            request.ContentType,
            cancellationToken);

        shop.BannerUrl = bannerUrl;
        shop.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(bannerUrl);
    }
}