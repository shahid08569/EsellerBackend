using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Search.GetSuggestions;

public class GetSuggestionsQueryHandler
    : IRequestHandler<GetSuggestionsQuery, Result<SearchSuggestionsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSuggestionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SearchSuggestionsDto>> Handle(
        GetSuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Trim().Length < 2)
            return Result.Success(new SearchSuggestionsDto(
                new List<AutocompleteDto>(),
                new List<AutocompleteDto>(),
                new List<AutocompleteDto>()));

        var searchTerm = request.Query.Trim().ToLower();

        var products = await _context.Products
            .Where(p => p.IsActive
                     && p.IsApproved
                     && p.DeletedAt == null
                     && p.Name.ToLower().Contains(searchTerm))
            .OrderByDescending(p => p.ViewCount)
            .Take(5)
            .Select(p => new AutocompleteDto(
                p.Name, "Product", p.Id, p.Slug, null))
            .ToListAsync(cancellationToken);

        var categories = await _context.Categories
            .Where(c => c.IsActive && c.Name.ToLower().Contains(searchTerm))
            .Take(5)
            .Select(c => new AutocompleteDto(
                c.Name, "Category", c.Id, c.Slug, null))
            .ToListAsync(cancellationToken);

        var brands = await _context.Brands
            .Where(b => b.IsActive && b.Name.ToLower().Contains(searchTerm))
            .Take(5)
            .Select(b => new AutocompleteDto(
                b.Name, "Brand", b.Id, b.Slug, b.LogoUrl))
            .ToListAsync(cancellationToken);

        return Result.Success(new SearchSuggestionsDto(products, categories, brands));
    }
}