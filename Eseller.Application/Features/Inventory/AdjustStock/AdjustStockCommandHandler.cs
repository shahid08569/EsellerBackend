using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Inventory.AdjustStock;

public class AdjustStockCommandHandler
    : IRequestHandler<AdjustStockCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly IAppNotificationService _notifications;

    public AdjustStockCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime,
        IAppNotificationService notifications)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
        _notifications = notifications;
    }

    public async Task<Result> Handle(
        AdjustStockCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null, cancellationToken);

        if (product is null)
            return Result.Failure("Product not found.", "PRODUCT_NOT_FOUND");

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
                return Result.Failure("You do not have permission.", "FORBIDDEN");
        }

        var variant = await _context.ProductVariants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pv => pv.Id == request.VariantId
                                    && pv.ProductId == request.ProductId
                                    && pv.DeletedAt == null,
                cancellationToken);

        if (variant is null)
            return Result.Failure("Variant not found.", "VARIANT_NOT_FOUND");

        // Check if there's a change
        var previousQuantity = variant.StockQty;
        var difference = request.NewQuantity - previousQuantity;

        if (difference == 0)
            return Result.Failure("New quantity is same as current quantity.", "NO_CHANGE");

        // Update variant stock
        variant.StockQty = request.NewQuantity;
        variant.SetUpdatedAt(_dateTime.UtcNow);

        // Create inventory transaction
        var transaction = new InventoryTransaction
        {
            ProductVariantId = variant.Id,
            TransactionType = InventoryTransactionType.ManualAdjustment,
            Quantity = difference,
            PreviousQuantity = previousQuantity,
            NewQuantity = request.NewQuantity,
            ReferenceType = "ManualAdjustment",
            ReferenceId = null,
            CreatedByAccountId = _currentUserService.AccountId.Value,
            CreatedAt = _dateTime.UtcNow
        };

        _context.InventoryTransactions.Add(transaction);

        await _context.SaveChangesAsync(cancellationToken);

        if (previousQuantity > 0 && request.NewQuantity <= 0)
        {
            await _notifications.NotifyProductOutOfStockOnceAsync(
                product.Id, product.Name, cancellationToken);
        }
        else if (previousQuantity <= 0 && request.NewQuantity > 0)
        {
            await _notifications.ResolveProductStockAlertAsync(product.Id, cancellationToken);
        }

        return Result.Success();
    }
}