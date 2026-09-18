using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Brands.DeleteBrand;

public class DeleteBrandCommandHandler
    : IRequestHandler<DeleteBrandCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDateTime _dateTime;

    public DeleteBrandCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        IDateTime dateTime)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        DeleteBrandCommand request,
        CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .FirstOrDefaultAsync(b => b.Id == request.BrandId, cancellationToken);

        if (brand is null)
            return Result.Failure("Brand not found.", "BRAND_NOT_FOUND");

        // Data Integrity: check if products exist
        var hasProducts = await _context.Products
            .AnyAsync(p => p.BrandId == request.BrandId, cancellationToken);

        if (hasProducts)
            return Result.Failure(
                "Cannot delete brand with products. Move or delete products first.",
                "BRAND_HAS_PRODUCTS");

        // Delete logo if exists
        if (!string.IsNullOrEmpty(brand.LogoUrl))
        {
            await _fileStorageService.DeleteAsync(brand.LogoUrl, cancellationToken);
        }

        // Soft delete
        brand.IsActive = false;
        brand.DeletedAt = _dateTime.UtcNow;
        brand.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}