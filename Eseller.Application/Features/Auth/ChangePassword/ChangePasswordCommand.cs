using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.ChangePassword;

public record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword) : IRequest<Result>;