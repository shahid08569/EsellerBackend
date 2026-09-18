using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Brands.UploadBrandLogo;

public class UploadBrandLogoCommandHandler
    : IRequestHandler<UploadBrandLogoCommand, Result<string>>
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDateTime _dateTime;

    public UploadBrandLogoCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        IDateTime dateTime)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _dateTime = dateTime;
    }

    public async Task<Result<string>> Handle(
        UploadBrandLogoCommand request,
        CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .FirstOrDefaultAsync(b => b.Id == request.BrandId, cancellationToken);

        if (brand is null)
            return Result.Failure<string>("Brand not found.", "BRAND_NOT_FOUND");

        if (request.FileSize > MaxFileSize)
            return Result.Failure<string>("File size exceeds 5 MB limit.", "FILE_TOO_LARGE");

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return Result.Failure<string>(
                "Invalid file type. Allowed: jpg, jpeg, png, webp.",
                "INVALID_FILE_TYPE");

        // Delete old logo
        if (!string.IsNullOrEmpty(brand.LogoUrl))
        {
            await _fileStorageService.DeleteAsync(brand.LogoUrl, cancellationToken);
        }

        // Upload new logo
        var uniqueFileName = $"brands/{brand.Id}/logo-{Guid.NewGuid()}{extension}";
        var logoUrl = await _fileStorageService.UploadAsync(
            request.FileStream,
            uniqueFileName,
            request.ContentType,
            cancellationToken);

        brand.LogoUrl = logoUrl;
        brand.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(logoUrl);
    }
}