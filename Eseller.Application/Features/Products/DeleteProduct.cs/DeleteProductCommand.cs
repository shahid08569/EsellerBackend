using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.DeleteProduct;

public record DeleteProductCommand(Guid ProductId) : IRequest<Result>;