using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Dashboard.UpdateProfile;

public record UpdateProfileCommand(
    string Name,
    string? Phone) : IRequest<Result>;