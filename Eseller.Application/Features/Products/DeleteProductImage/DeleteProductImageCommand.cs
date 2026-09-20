using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.DeleteProductImage;

public record DeleteProductImageCommand(
    Guid ProductId,
    Guid ImageId) : IRequest<Result>;