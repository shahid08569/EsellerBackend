using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Dashboard.DeleteAddress;

public record DeleteAddressCommand(Guid AddressId) : IRequest<Result>;