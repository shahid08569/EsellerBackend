using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Brands.CreateBrand;

public record CreateBrandCommand(
    string Name,
    string? LogoUrl = null,
    bool IsActive = true) : IRequest<Result<Guid>>;
