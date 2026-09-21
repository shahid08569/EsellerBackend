using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Dashboard.GetAddresses;

public record GetAddressesQuery : IRequest<Result<IReadOnlyList<AddressDto>>>;