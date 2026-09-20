using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.GetProductBySlug;

public record GetProductBySlugQuery(string Slug) : IRequest<Result<ProductDto>>;