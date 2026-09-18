using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Admin.Shopkeepers.GetShopkeepers;

public class GetShopkeepersQueryHandler
    : IRequestHandler<GetShopkeepersQuery, Result<IReadOnlyList<ShopkeeperDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetShopkeepersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<ShopkeeperDto>>> Handle(
        GetShopkeepersQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Base query
        var query = _context.Shopkeepers.AsQueryable();

        // 2. Filter by status
        if (request.Status.HasValue)
            query = query.Where(s => s.Status == request.Status.Value);

        // 3. Get shopkeepers
        var shopkeepers = await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        if (!shopkeepers.Any())
            return Result.Success<IReadOnlyList<ShopkeeperDto>>(new List<ShopkeeperDto>());

        var accountIds = shopkeepers.Select(s => s.AccountId).ToList();
        var shopkeeperIds = shopkeepers.Select(s => s.Id).ToList();

        // 4. Get accounts (for email)
        var accounts = await _context.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Email, cancellationToken);

        // 5. Get shops (for store details)
        var shops = await _context.Shops
            .Where(s => shopkeeperIds.Contains(s.ShopkeeperId))
            .ToDictionaryAsync(s => s.ShopkeeperId, s => s, cancellationToken);

        // 6. Map
        var result = shopkeepers.Select(s =>
        {
            var shop = shops.GetValueOrDefault(s.Id);

            return new ShopkeeperDto(
                s.Id,
                s.AccountId,
                s.Name,
                accounts.GetValueOrDefault(s.AccountId, string.Empty),
                s.Phone,
                shop?.Name ?? string.Empty,
                shop?.Slug ?? string.Empty,
                shop?.City ?? string.Empty,
                shop?.Country ?? string.Empty,
                s.Status.ToString(),
                s.ApprovedAt,
                s.RejectionReason,
                s.CreatedAt);
        }).ToList();

        return Result.Success<IReadOnlyList<ShopkeeperDto>>(result);
    }
}