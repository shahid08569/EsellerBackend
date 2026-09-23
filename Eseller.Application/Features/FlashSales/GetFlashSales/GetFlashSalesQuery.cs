using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.FlashSales.GetFlashSales;

public record GetFlashSalesQuery(
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<FlashSaleDto>>>;