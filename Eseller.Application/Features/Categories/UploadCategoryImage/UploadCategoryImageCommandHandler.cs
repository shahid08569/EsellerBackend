using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Categories.UploadCategoryImage;

public class UploadCategoryImageCommandHandler
    : IRequestHandler<UploadCategoryImageCommand, Result<string>>
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDateTime _dateTime;

    public UploadCategoryImageCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        IDateTime dateTime)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _dateTime = dateTime;
    }

    public async Task<Result<string>> Handle(
        UploadCategoryImageCommand request,
        CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
            return Result.Failure<string>("Category not found.", "CATEGORY_NOT_FOUND");

        if (request.FileSize > MaxFileSize)
            return Result.Failure<string>("File size exceeds 5 MB limit.", "FILE_TOO_LARGE");

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return Result.Failure<string>(
                "Invalid file type. Allowed: jpg, jpeg, png, webp.",
                "INVALID_FILE_TYPE");

        // Delete old image
        if (!string.IsNullOrEmpty(category.ImageUrl))
        {
            await _fileStorageService.DeleteAsync(category.ImageUrl, cancellationToken);
        }

        // Upload new image
        var uniqueFileName = $"categories/{category.Id}/image-{Guid.NewGuid()}{extension}";
        var imageUrl = await _fileStorageService.UploadAsync(
            request.FileStream,
            uniqueFileName,
            request.ContentType,
            cancellationToken);

        category.ImageUrl = imageUrl;
        category.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(imageUrl);
    }
}