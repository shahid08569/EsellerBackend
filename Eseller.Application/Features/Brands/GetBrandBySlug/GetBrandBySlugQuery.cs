using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Brands.GetBrandBySlug;

public record GetBrandBySlugQuery(string Slug) : IRequest<Result<BrandDto>>;