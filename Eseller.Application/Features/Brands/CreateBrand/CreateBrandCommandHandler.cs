using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Brands.CreateBrand;

public class CreateBrandCommandHandler
    : IRequestHandler<CreateBrandCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public CreateBrandCommandHandler(
        IApplicationDbContext context,
        IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        CreateBrandCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Generate slug
        var slug = GenerateSlug(request.Name);

        // 2. Reuse soft-deleted brand with same slug, else enforce uniqueness
        var existing = await _context.Brands
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Slug == slug, cancellationToken);

        if (existing is not null)
        {
            if (existing.DeletedAt is null)
                return Result.Failure<Guid>(
                    "A brand with this name already exists.",
                    "BRAND_NAME_TAKEN");

            existing.Name = request.Name.Trim();
            existing.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? existing.LogoUrl : request.LogoUrl.Trim();
            existing.IsActive = request.IsActive;
            existing.DeletedAt = null;
            existing.SetUpdatedAt(_dateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success(existing.Id);
        }

        // 3. Create brand
        var brand = new Brand
        {
            Name = request.Name,
            Slug = slug,
            LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim(),
            IsActive = request.IsActive
        };

        _context.Brands.Add(brand);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(brand.Id);
    }

    private static string GenerateSlug(string name)
    {
        return name
            .ToLowerInvariant()
            .Trim()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .Aggregate(string.Empty, (current, c) => current + c)
            .Replace("--", "-");
    }
}