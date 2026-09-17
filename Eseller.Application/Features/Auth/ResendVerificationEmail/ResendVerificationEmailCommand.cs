using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.ResendVerificationEmail;

public record ResendVerificationEmailCommand(
    string Email) : IRequest<Result>;