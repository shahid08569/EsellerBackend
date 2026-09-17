using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.SendPhoneOtp;

public record SendPhoneOtpCommand(string Email) : IRequest<Result>;