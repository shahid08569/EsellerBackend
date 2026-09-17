using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.ForgotPassword;

public record ForgotPasswordCommand(string Email) : IRequest<Result>;