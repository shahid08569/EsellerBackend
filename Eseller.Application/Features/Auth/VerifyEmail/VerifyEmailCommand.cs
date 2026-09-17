using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.VerifyEmail;

public record VerifyEmailCommand(
    string Email,
    string Code) : IRequest<Result>;