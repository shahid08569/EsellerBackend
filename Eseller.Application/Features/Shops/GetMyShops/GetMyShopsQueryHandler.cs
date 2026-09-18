using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Shops.GetMyShops;

public class GetMyShopsQueryHandler
    : IRequestHandler<GetMyShopsQuery, Result<IReadOnlyList<ShopListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyShopsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<ShopListDto>>> Handle(
        GetMyShopsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure<IReadOnlyList<ShopListDto>>(
                "User is not authenticated.",
                "UNAUTHENTICATED");

        // 2. Find shopkeeper
        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (shopkeeper is null)
            return Result.Success<IReadOnlyList<ShopListDto>>(new List<ShopListDto>());

        // 3. Get all shops of this shopkeeper (including inactive — with DeletedAt = null)
        var shops = await _context.Shops
            .IgnoreQueryFilters()
            .Where(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ShopListDto(
                s.Id,
                s.Name,
                s.Slug,
                s.LogoUrl,
                s.City,
                s.Country,
                s.Rating,
                s.TotalProducts,
                s.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ShopListDto>>(shops);
    }
}