using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Search.GetAutocomplete;

public class GetAutocompleteQueryHandler
    : IRequestHandler<GetAutocompleteQuery, Result<List<AutocompleteDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAutocompleteQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AutocompleteDto>>> Handle(
        GetAutocompleteQuery request,
        CancellationToken cancellationToken)
    {
        // Minimum 2 characters
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Trim().Length < 2)
            return Result.Success(new List<AutocompleteDto>());

        var searchTerm = request.Query.Trim().ToLower();
        var limit = request.Limit > 20 ? 20 : request.Limit;

        // Products (top 5)
        var products = await _context.Products
            .Where(p => p.IsActive
                     && p.IsApproved
                     && p.DeletedAt == null
                     && p.Name.ToLower().Contains(searchTerm))
            .OrderByDescending(p => p.ViewCount)
            .Take(5)
            .Select(p => new AutocompleteDto(
                p.Name,
                "Product",
                p.Id,
                p.Slug,
                null))
            .ToListAsync(cancellationToken);

        // Categories (top 3)
        var categories = await _context.Categories
            .Where(c => c.IsActive
                     && c.Name.ToLower().Contains(searchTerm))
            .Take(3)
            .Select(c => new AutocompleteDto(
                c.Name,
                "Category",
                c.Id,
                c.Slug,
                null))
            .ToListAsync(cancellationToken);

        // Brands (top 2)
        var brands = await _context.Brands
            .Where(b => b.IsActive
                     && b.Name.ToLower().Contains(searchTerm))
            .Take(2)
            .Select(b => new AutocompleteDto(
                b.Name,
                "Brand",
                b.Id,
                b.Slug,
                b.LogoUrl))
            .ToListAsync(cancellationToken);

        var result = products
            .Concat(categories)
            .Concat(brands)
            .Take(limit)
            .ToList();

        return Result.Success(result);
    }
}