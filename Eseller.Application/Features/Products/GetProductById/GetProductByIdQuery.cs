using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.GetProductById;

public record GetProductByIdQuery(Guid ProductId) : IRequest<Result<ProductDto>>;