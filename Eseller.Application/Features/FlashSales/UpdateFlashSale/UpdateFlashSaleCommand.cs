using Eseller.Application.Common;
using Eseller.Application.Features.FlashSales.CreateFlashSale;
using MediatR;

namespace Eseller.Application.Features.FlashSales.UpdateFlashSale;

public record UpdateFlashSaleCommand(
    Guid FlashSaleId,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    List<FlashSaleProductInput> Products) : IRequest<Result>;