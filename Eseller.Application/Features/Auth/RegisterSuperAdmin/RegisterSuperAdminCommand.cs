using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.RegisterSuperAdmin;

public record RegisterSuperAdminCommand(
    string Name,
    string Email,
    string Password,
    decimal Latitude,
    decimal Longitude,
    string? DeviceType
) : IRequest<Result<RegisterSuperAdminResponse>>;

public record RegisterSuperAdminResponse(
    Guid AccountId,
    string Email,
    string Message);
