using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.Logout;

public record LogoutCommand(string RefreshToken) : IRequest<Result>;