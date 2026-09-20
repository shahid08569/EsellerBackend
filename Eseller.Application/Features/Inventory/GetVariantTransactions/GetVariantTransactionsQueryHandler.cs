using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Inventory.GetVariantTransactions;

public class GetVariantTransactionsQueryHandler
    : IRequestHandler<GetVariantTransactionsQuery, Result<PagedList<InventoryTransactionDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetVariantTransactionsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<InventoryTransactionDto>>> Handle(
        GetVariantTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<InventoryTransactionDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null, cancellationToken);

        if (product is null)
            return Result.Failure<PagedList<InventoryTransactionDto>>(
                "Product not found.", "PRODUCT_NOT_FOUND");

        // Permission check
        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        if (!isAdmin)
        {
            var shop = await _context.Shops
                .FirstOrDefaultAsync(s => s.Id == product.ShopId, cancellationToken);

            var shopkeeper = shop is null ? null : await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

            if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                return Result.Failure<PagedList<InventoryTransactionDto>>(
                    "You do not have permission.", "FORBIDDEN");
        }

        var variant = await _context.ProductVariants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pv => pv.Id == request.VariantId
                                    && pv.ProductId == request.ProductId,
                cancellationToken);

        if (variant is null)
            return Result.Failure<PagedList<InventoryTransactionDto>>(
                "Variant not found.", "VARIANT_NOT_FOUND");

        var query = _context.InventoryTransactions
            .Where(it => it.ProductVariantId == request.VariantId)
            .OrderByDescending(it => it.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var transactions = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(it => new InventoryTransactionDto(
                it.Id,
                it.ProductVariantId,
                it.TransactionType.ToString(),
                it.Quantity,
                it.PreviousQuantity,
                it.NewQuantity,
                it.ReferenceType,
                it.ReferenceId,
                it.CreatedByAccountId,
                it.CreatedAt))
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<InventoryTransactionDto>(
            transactions,
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result.Success(pagedList);
    }
}