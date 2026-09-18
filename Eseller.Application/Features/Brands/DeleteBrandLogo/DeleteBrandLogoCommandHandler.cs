using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Brands.DeleteBrandLogo;

public class DeleteBrandLogoCommandHandler
    : IRequestHandler<DeleteBrandLogoCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDateTime _dateTime;

    public DeleteBrandLogoCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        IDateTime dateTime)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        DeleteBrandLogoCommand request,
        CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .FirstOrDefaultAsync(b => b.Id == request.BrandId, cancellationToken);

        if (brand is null)
            return Result.Failure("Brand not found.", "BRAND_NOT_FOUND");

        if (!string.IsNullOrEmpty(brand.LogoUrl))
        {
            await _fileStorageService.DeleteAsync(brand.LogoUrl, cancellationToken);
            brand.LogoUrl = null;
            brand.SetUpdatedAt(_dateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}