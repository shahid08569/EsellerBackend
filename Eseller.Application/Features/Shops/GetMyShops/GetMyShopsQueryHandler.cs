using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Application.Features.Products;
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
        if (_currentUserService.AccountId is null)
            return Result.Failure<IReadOnlyList<ShopListDto>>(
                "User is not authenticated.",
                "UNAUTHENTICATED");

        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (shopkeeper is null)
            return Result.Success<IReadOnlyList<ShopListDto>>(new List<ShopListDto>());

        var isApproved = shopkeeper.Status == Domain.Enums.ShopkeeperApprovalStatus.Approved;
        var statusStr = shopkeeper.Status.ToString();

        var shops = await _context.Shops
            .IgnoreQueryFilters()
            .Where(s => s.ShopkeeperId == shopkeeper.Id && s.DeletedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var categoryIds = shops
            .Where(s => s.ShopCategoryId.HasValue)
            .Select(s => s.ShopCategoryId!.Value)
            .Distinct()
            .ToList();

        var categories = await _context.ShopCategories
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id) && c.DeletedAt == null)
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var result = shops.Select(s =>
        {
            categories.TryGetValue(s.ShopCategoryId ?? Guid.Empty, out var tier);
            var maxLimit = tier?.MaxProductListings > 0
                ? tier.MaxProductListings
                : MasterCatalog.DefaultFreeProductLimit;
            var tierName = !string.IsNullOrWhiteSpace(s.CustomBadgeText)
                ? s.CustomBadgeText
                : tier?.BadgeText ?? tier?.Name ?? "Bronze (Free)";

            return new ShopListDto(
                s.Id,
                s.Name,
                s.Slug,
                s.LogoUrl,
                s.City,
                s.Country,
                s.Rating,
                s.TotalProducts,
                s.CreatedAt,
                s.ShopCategoryId,
                tier?.Name,
                tierName,
                tier?.BadgeColor,
                tier?.IconUrl,
                isApproved,
                s.IsActive,
                statusStr,
                s.BannerUrl,
                maxLimit,
                tierName,
                tier?.PriceUsd ?? 0);
        }).ToList();

        return Result.Success<IReadOnlyList<ShopListDto>>(result);
    }
}
