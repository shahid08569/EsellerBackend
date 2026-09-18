using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Brands.DeleteBrand;

public record DeleteBrandCommand(Guid BrandId) : IRequest<Result>;