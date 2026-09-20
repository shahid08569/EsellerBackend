using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.UpdateProductImageSort;

public record UpdateProductImageSortCommand(
    Guid ProductId,
    Guid ImageId,
    int NewSortOrder) : IRequest<Result>;