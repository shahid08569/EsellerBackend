using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Dashboard.CreateAddress;

public record CreateAddressCommand(
    string Label,
    string FullName,
    string Phone,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? State,
    string Country,
    string? PostalCode,
    bool IsDefault) : IRequest<Result<Guid>>;