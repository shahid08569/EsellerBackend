using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.ResetPassword;

public record ResetPasswordCommand(
    string Email,
    string Code,
    string NewPassword) : IRequest<Result>;