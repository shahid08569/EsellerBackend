using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Brands.GetBrandById;

public class GetBrandByIdQueryHandler
    : IRequestHandler<GetBrandByIdQuery, Result<BrandDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBrandByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BrandDto>> Handle(
        GetBrandByIdQuery request,
        CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .FirstOrDefaultAsync(b => b.Id == request.BrandId && b.IsActive, cancellationToken);

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