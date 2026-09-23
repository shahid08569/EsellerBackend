using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.FlashSales.CreateFlashSale;

public record CreateFlashSaleCommand(
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    List<FlashSaleProductInput> Products) : IRequest<Result<Guid>>;

public record FlashSaleProductInput(
    Guid ProductId,
    int DiscountType,
    decimal DiscountValue);