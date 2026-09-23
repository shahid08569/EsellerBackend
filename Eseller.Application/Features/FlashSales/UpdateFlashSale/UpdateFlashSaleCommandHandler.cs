using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Promotions;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.FlashSales.UpdateFlashSale;

public class UpdateFlashSaleCommandHandler
    : IRequestHandler<UpdateFlashSaleCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public UpdateFlashSaleCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdateFlashSaleCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can update flash sales.", "FORBIDDEN");

        var flashSale = await _context.FlashSales
            .FirstOrDefaultAsync(f => f.Id == request.FlashSaleId, cancellationToken);

        if (flashSale is null)
            return Result.Failure("Flash sale not found.", "FLASHSALE_NOT_FOUND");

        // Validate products exist
        var productIds = request.Products.Select(p => p.ProductId).Distinct().ToList();

        var existingProductIds = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id)
                     && p.IsActive
                     && p.IsApproved
                     && p.DeletedAt == null)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (productIds.Except(existingProductIds).Any())
            return Result.Failure(
                "One or more products are not available.",
                "PRODUCTS_NOT_AVAILABLE");

        var now = _dateTime.UtcNow;

        flashSale.Name = request.Name.Trim();
        flashSale.StartDate = request.StartDate;
        flashSale.EndDate = request.EndDate;
        flashSale.IsActive = request.IsActive;
        flashSale.SetUpdatedAt(now);

        // Remove existing products
        var existingItems = await _context.FlashSaleProducts
            .Where(fp => fp.FlashSaleId == flashSale.Id)
            .ToListAsync(cancellationToken);

        _context.FlashSaleProducts.RemoveRange(existingItems);

        // Add new products
        foreach (var product in request.Products)
        {
            _context.FlashSaleProducts.Add(new FlashSaleProduct
            {
                FlashSaleId = flashSale.Id,
                ProductId = product.ProductId,
                DiscountType = (DiscountType)product.DiscountType,
                DiscountValue = product.DiscountValue
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}