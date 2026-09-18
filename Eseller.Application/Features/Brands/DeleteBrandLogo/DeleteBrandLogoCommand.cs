using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Brands.DeleteBrandLogo;

public record DeleteBrandLogoCommand(Guid BrandId) : IRequest<Result>;