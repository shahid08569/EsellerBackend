using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Promotions;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.FlashSales.CreateFlashSale;

public class CreateFlashSaleCommandHandler
    : IRequestHandler<CreateFlashSaleCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public CreateFlashSaleCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        CreateFlashSaleCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<Guid>(
                "Only admins can create flash sales.", "FORBIDDEN");

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

        var missingProducts = productIds.Except(existingProductIds).ToList();

        if (missingProducts.Count > 0)
            return Result.Failure<Guid>(
                "One or more products are not available.",
                "PRODUCTS_NOT_AVAILABLE");

        // SuperAdmin may flash-sale any active approved catalog product
        // (master warehouse SKUs do not need a seller listing first).

        var now = _dateTime.UtcNow;

        static DateTime ToUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        var startDate = ToUtc(request.StartDate);
        var endDate = ToUtc(request.EndDate);

        // Avoid "not started yet" when admin picks "now" in local time ahead of UTC
        if (startDate > now && startDate <= now.AddMinutes(30))
            startDate = now;

        if (endDate <= startDate)
            endDate = startDate.AddDays(3);

        var flashSale = new FlashSale
        {
            Name = request.Name.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            IsActive = true
        };

        flashSale.SetCreatedAt(now);
        flashSale.SetUpdatedAt(now);

        _context.FlashSales.Add(flashSale);

        await _context.SaveChangesAsync(cancellationToken);

        // Add products
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

        return Result.Success(flashSale.Id);
    }
}