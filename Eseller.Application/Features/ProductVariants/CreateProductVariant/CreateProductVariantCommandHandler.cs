using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ProductVariants.CreateProductVariant;

public class CreateProductVariantCommandHandler
    : IRequestHandler<CreateProductVariantCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public CreateProductVariantCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        CreateProductVariantCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        // Find product
        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null, cancellationToken);

        if (product is null)
            return Result.Failure<Guid>("Product not found.", "PRODUCT_NOT_FOUND");

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
                return Result.Failure<Guid>("You do not have permission.", "FORBIDDEN");
        }

        // Check SKU uniqueness
        var skuExists = await _context.ProductVariants
            .AnyAsync(pv => pv.Sku == request.Sku, cancellationToken);

        if (skuExists)
            return Result.Failure<Guid>("SKU is already taken.", "SKU_TAKEN");

        // Create variant
        var variant = new ProductVariant
        {
            ProductId = request.ProductId,
            Sku = request.Sku,
            Price = request.Price,
            StockQty = request.StockQty,
            LowStockThreshold = request.LowStockThreshold,
            IsActive = true
        };

        _context.ProductVariants.Add(variant);

        // Create attribute values
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

        return Result.Success(variant.Id);
    }
}