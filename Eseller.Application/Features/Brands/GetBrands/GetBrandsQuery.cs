using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Brands.GetBrands;

public record GetBrandsQuery : IRequest<Result<IReadOnlyList<BrandDto>>>;