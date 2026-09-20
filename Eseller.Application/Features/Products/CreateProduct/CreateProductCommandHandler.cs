using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
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

        var shop = await _context.Shops
            .FirstOrDefaultAsync(s => s.Id == request.ShopId, cancellationToken);

        if (shop is null)
            return Result.Failure<Guid>("Shop not found.", "SHOP_NOT_FOUND");

        var shopkeeper = await _context.Shopkeepers
            .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

        if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
            return Result.Failure<Guid>("You do not have permission.", "FORBIDDEN");

        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken);

        if (!categoryExists)
            return Result.Failure<Guid>("Category not found.", "CATEGORY_NOT_FOUND");

        if (request.BrandId.HasValue)
        {
            var brandExists = await _context.Brands
                .AnyAsync(b => b.Id == request.BrandId.Value && b.IsActive, cancellationToken);

            if (!brandExists)
                return Result.Failure<Guid>("Brand not found.", "BRAND_NOT_FOUND");
        }

        var slug = await GenerateUniqueSlugAsync(request.Name, cancellationToken);

        // Check approval setting
        var requireApprovalSetting = await _context.Settings
            .FirstOrDefaultAsync(s => s.Key == "RequireProductApproval", cancellationToken);

        var requireApproval = requireApprovalSetting?.Value?.ToLower() == "true";

        var product = new Product
        {
            ShopId = request.ShopId,
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            Name = request.Name,
            Slug = slug,
            Description = request.Description,
            BasePrice = request.BasePrice,
            IsFeatured = false,
            IsApproved = !requireApproval,
            ViewCount = 0,
            SeoTitle = request.SeoTitle,
            SeoDescription = request.SeoDescription,
            IsActive = true
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(product.Id);
    }

    private async Task<string> GenerateUniqueSlugAsync(
        string name, CancellationToken cancellationToken)
    {
        var baseSlug = name.ToLowerInvariant().Trim()
            .Replace(" ", "-").Replace("_", "-")
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .Aggregate(string.Empty, (current, c) => current + c)
            .Replace("--", "-");

        var slug = baseSlug;
        var counter = 1;

        while (await _context.Products.AnyAsync(p => p.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }

        return slug;
    }
}