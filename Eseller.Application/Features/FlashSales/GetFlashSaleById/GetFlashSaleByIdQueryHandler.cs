using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.FlashSales.GetFlashSaleById;

public class GetFlashSaleByIdQueryHandler
    : IRequestHandler<GetFlashSaleByIdQuery, Result<FlashSaleDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public GetFlashSaleByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<FlashSaleDto>> Handle(
        GetFlashSaleByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<FlashSaleDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<FlashSaleDto>(
                "Only admins can view flash sales.", "FORBIDDEN");

        var flashSale = await _context.FlashSales
            .FirstOrDefaultAsync(f => f.Id == request.FlashSaleId, cancellationToken);

        if (flashSale is null)
            return Result.Failure<FlashSaleDto>(
                "Flash sale not found.", "FLASHSALE_NOT_FOUND");

        var items = await _context.FlashSaleProducts
            .Where(fp => fp.FlashSaleId == flashSale.Id)
            .ToListAsync(cancellationToken);

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var productDtos = items.Select(i => new FlashSaleProductDto(
            i.Id,
            i.ProductId,
            products.GetValueOrDefault(i.ProductId, string.Empty),
            (int)i.DiscountType,
            i.DiscountValue)).ToList();

        var now = _dateTime.UtcNow;
        var isCurrentlyActive = flashSale.IsActive
            && flashSale.StartDate <= now
            && flashSale.EndDate >= now;

        var dto = new FlashSaleDto(
            flashSale.Id,
            flashSale.Name,
            flashSale.StartDate,
            flashSale.EndDate,
            flashSale.IsActive,
            isCurrentlyActive,
            productDtos.Count,
            productDtos,
            flashSale.CreatedAt,
            flashSale.UpdatedAt);

        return Result.Success(dto);
    }
}