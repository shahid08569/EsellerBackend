using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Brands.UpdateBrand;

public class UpdateBrandCommandHandler
    : IRequestHandler<UpdateBrandCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public UpdateBrandCommandHandler(
        IApplicationDbContext context,
        IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdateBrandCommand request,
        CancellationToken cancellationToken)
    {
        var brand = await _context.Brands
            .FirstOrDefaultAsync(b => b.Id == request.BrandId, cancellationToken);

        if (brand is null)
            return Result.Failure("Brand not found.", "BRAND_NOT_FOUND");

        brand.Name = request.Name;
        brand.IsActive = request.IsActive;
        brand.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}