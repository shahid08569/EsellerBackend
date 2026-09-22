using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Search.GetTrendingSearches;

public class GetTrendingSearchesQueryHandler
    : IRequestHandler<GetTrendingSearchesQuery, Result<List<string>>>
{
    private readonly IApplicationDbContext _context;

    public GetTrendingSearchesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<string>>> Handle(
        GetTrendingSearchesQuery request,
        CancellationToken cancellationToken)
    {
        // Top viewed products names — trending searches
        var trending = await _context.Products
            .Where(p => p.IsActive
                     && p.IsApproved
                     && p.DeletedAt == null
                     && p.ViewCount > 0)
            .OrderByDescending(p => p.ViewCount)
            .Take(request.Limit > 20 ? 20 : request.Limit)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        return Result.Success(trending);
    }
}