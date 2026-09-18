using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Brands.GetBrandBySlug;

public class GetBrandBySlugQueryHandler
    : IRequestHandler<GetBrandBySlugQuery, Result<BrandDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBrandBySlugQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BrandDto>> Handle(
        GetBrandBySlugQuery request,
        CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .FirstOrDefaultAsync(b => b.Slug == request.Slug && b.IsActive, cancellationToken);

        if (brand is null)
            return Result.Failure<BrandDto>("Brand not found.", "BRAND_NOT_FOUND");

        var dto = new BrandDto(
            brand.Id,
            brand.Name,
            brand.Slug,
            brand.LogoUrl,
            brand.IsActive,
            brand.CreatedAt);

        return Result.Success(dto);
    }
}