using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ProductVariants.UpdateProductVariant;

public class UpdateProductVariantCommandHandler
    : IRequestHandler<UpdateProductVariantCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public UpdateProductVariantCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdateProductVariantCommand request,
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

        // Find variant
        var variant = await _context.ProductVariants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pv => pv.Id == request.VariantId
                                    && pv.ProductId == request.ProductId
                                    && pv.DeletedAt == null,
                cancellationToken);

        if (variant is null)
            return Result.Failure("Variant not found.", "VARIANT_NOT_FOUND");

        // Check SKU uniqueness (if changed)
        if (variant.Sku != request.Sku)
        {
            var skuExists = await _context.ProductVariants
                .AnyAsync(pv => pv.Sku == request.Sku && pv.Id != request.VariantId, cancellationToken);

            if (skuExists)
                return Result.Failure("SKU is already taken.", "SKU_TAKEN");
        }

        // Update variant
        variant.Sku = request.Sku;
        variant.Price = request.Price;
        variant.StockQty = request.StockQty;
        variant.LowStockThreshold = request.LowStockThreshold;
        variant.IsActive = request.IsActive;
        variant.SetUpdatedAt(_dateTime.UtcNow);

        // Remove existing attributes
        var existingAttributes = await _context.VariantAttributeValues
            .Where(vav => vav.ProductVariantId == variant.Id)
            .ToListAsync(cancellationToken);

        _context.VariantAttributeValues.RemoveRange(existingAttributes);

        // Add new attributes
        foreach (var attr in request.Attributes)
        {
            _context.VariantAttributeValues.Add(new VariantAttributeValue
            {
                ProductVariantId = variant.Id,
                AttributeName = attr.AttributeName,
                AttributeValue = attr.AttributeValue
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}