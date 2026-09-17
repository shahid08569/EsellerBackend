using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.RegisterCustomer;

public record RegisterCustomerCommand(
    string Name,
    string Email,
    string Phone,
    string Password,
    decimal Latitude,
    decimal Longitude,
    string DeviceType) : IRequest<Result<RegisterCustomerResponse>>;