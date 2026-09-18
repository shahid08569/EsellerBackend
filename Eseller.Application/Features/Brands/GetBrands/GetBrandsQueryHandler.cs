using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Brands.GetBrands;

public class GetBrandsQueryHandler
    : IRequestHandler<GetBrandsQuery, Result<IReadOnlyList<BrandDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetBrandsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<BrandDto>>> Handle(
        GetBrandsQuery request,
        CancellationToken cancellationToken)
    {
        var brands = await _context.Brands
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new BrandDto(
                b.Id,
                b.Name,
                b.Slug,
                b.LogoUrl,
                b.IsActive,
                b.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BrandDto>>(brands);
    }
}