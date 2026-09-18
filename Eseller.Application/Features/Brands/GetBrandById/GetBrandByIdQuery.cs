using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Brands.GetBrandById;

public record GetBrandByIdQuery(Guid BrandId) : IRequest<Result<BrandDto>>;