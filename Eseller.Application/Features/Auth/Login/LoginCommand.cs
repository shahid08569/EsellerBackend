using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.Login;

public record LoginCommand(
    string UsernameOrEmail,
    string Password,
    decimal Latitude,
    decimal Longitude,
    string DeviceType) : IRequest<Result<LoginResponse>>;