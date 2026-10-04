using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Brands.UpdateBrand;

public record UpdateBrandCommand(
    Guid BrandId,
    string Name,
    bool IsActive,
    string? LogoUrl = null) : IRequest<Result>;
