using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Banners.GetActiveBanners;

public class GetActiveBannersQueryHandler
    : IRequestHandler<GetActiveBannersQuery, Result<List<HomepageBannerDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public GetActiveBannersQueryHandler(
        IApplicationDbContext context,
        IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<Result<List<HomepageBannerDto>>> Handle(
        GetActiveBannersQuery request,
        CancellationToken cancellationToken)
    {
        var now = _dateTime.UtcNow;

        var banners = await _context.HomepageBanners
            .Where(b => b.IsActive
                     && (!b.StartDate.HasValue || b.StartDate.Value <= now)
                     && (!b.EndDate.HasValue || b.EndDate.Value >= now))
            .OrderBy(b => b.SortOrder)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = banners.Select(b => new HomepageBannerDto(
            b.Id,
            b.ImageUrl,
            b.LinkUrl,
            b.SortOrder,
            b.IsActive,
            true,
            b.StartDate,
            b.EndDate,
            b.CreatedAt,
            b.UpdatedAt)).ToList();

        return Result.Success(result);
    }
}