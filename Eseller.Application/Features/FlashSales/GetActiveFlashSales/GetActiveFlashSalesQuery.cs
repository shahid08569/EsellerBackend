using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.FlashSales.GetActiveFlashSales;

public record GetActiveFlashSalesQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<FlashSaleDto>>>;