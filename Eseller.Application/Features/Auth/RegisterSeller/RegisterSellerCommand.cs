using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.RegisterSeller;

public record RegisterSellerCommand(
    string Name,
    string Email,
    string Phone,
    string Password,
    decimal Latitude,
    decimal Longitude,
    string DeviceType) : IRequest<Result<RegisterSellerResponse>>;