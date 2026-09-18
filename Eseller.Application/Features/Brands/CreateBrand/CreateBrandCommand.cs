using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Brands.CreateBrand;

public record CreateBrandCommand(string Name) : IRequest<Result<Guid>>;