using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.VerifyPhoneOtp;

public record VerifyPhoneOtpCommand(
    string Email,
    string Code) : IRequest<Result>;