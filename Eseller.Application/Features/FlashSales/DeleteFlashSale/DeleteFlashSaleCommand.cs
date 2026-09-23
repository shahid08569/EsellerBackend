using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.FlashSales.DeleteFlashSale;

public record DeleteFlashSaleCommand(Guid FlashSaleId) : IRequest<Result>;