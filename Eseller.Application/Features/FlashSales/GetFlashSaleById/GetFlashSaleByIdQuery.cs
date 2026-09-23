using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.FlashSales.GetFlashSaleById;

public record GetFlashSaleByIdQuery(Guid FlashSaleId) : IRequest<Result<FlashSaleDto>>;