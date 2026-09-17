using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.RefreshToken;
public record RefreshTokenCommand(
    string RefreshToken) : IRequest<Result<RefreshTokenResponse>>;