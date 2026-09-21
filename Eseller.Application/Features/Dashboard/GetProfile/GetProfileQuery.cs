using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Dashboard.GetProfile;

public record GetProfileQuery : IRequest<Result<UserProfileDto>>;