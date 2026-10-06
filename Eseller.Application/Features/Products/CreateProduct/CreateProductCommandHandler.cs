using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.CreateProduct;

public class CreateProductCommandHandler
    : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public CreateProductCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var isSuperAdminOrPartner = _currentUserService.RoleType == RoleType.SuperAdmin ||
                                    _currentUserService.RoleType == RoleType.Partner;

        if (!isSuperAdminOrPartner)
            return Result.Failure<Guid>(
                "Only Super Admin can create catalog products. Sellers must use Add to Store from the marketplace catalog.",
                "FORBIDDEN");

        // Admin catalog products always belong to the global Platform Master Warehouse.
        // Never assign to a seller shop — sellers clone from the marketplace catalog.
        Shop masterShop;
        try
        {
            masterShop = await MasterCatalog.GetOrCreateMasterShopAsync(_context, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<Guid>(
                $"Master warehouse is not ready: {ex.Message}",
                "MASTER_WAREHOUSE_ERROR");
        }

        var targetShopId = masterShop.Id;

        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken);

        if (!categoryExists)
            return Result.Failure<Guid>(
                "Category not found. Please select a valid category.",
                "CATEGORY_NOT_FOUND");

        if (request.BrandId.HasValue)
        {
            var brandExists = await _context.Brands
                .AnyAsync(b => b.Id == request.BrandId.Value && b.IsActive, cancellationToken);

            if (!brandExists)
                return Result.Failure<Guid>("Brand not found.", "BRAND_NOT_FOUND");
        }

        var slug = await ProductSlug.GenerateUniqueAsync(
            _context, request.Name, prefix: null, cancellationToken);
        var now = _dateTime.UtcNow;

        var product = new Product
        {
            ShopId = targetShopId,
            SourceProductId = null,
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description,
            BasePrice = request.BasePrice,
            IsFeatured = false,
            IsApproved = true,
            IsStorefrontLive = false,
            ViewCount = 0,
            SeoTitle = request.SeoTitle,
            SeoDescription = request.SeoDescription,
            IsActive = true
        };
        product.SetCreatedAt(now);
        product.SetUpdatedAt(now);

        _context.Products.Add(product);

        masterShop.TotalProducts = await _context.Products
            .IgnoreQueryFilters()
            .CountAsync(p =>
                p.ShopId == masterShop.Id
                && p.DeletedAt == null
                && p.SourceProductId == null, cancellationToken) + 1;
        masterShop.SetUpdatedAt(now);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ProductSlug.IsUniqueSlugViolation(ex))
        {
            // Race: another request claimed the same slug between check and insert.
            product.Slug = ProductSlug.WithUniqueSuffix(ProductSlug.Normalize(request.Name));
            product.SetUpdatedAt(_dateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(product.Id);
    }
}
