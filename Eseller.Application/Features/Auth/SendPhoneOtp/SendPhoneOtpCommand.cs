using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Auth.SendPhoneOtp;

public record SendPhoneOtpCommand(string Email) : IRequest<Result<SendPhoneOtpResponse>>;

public record SendPhoneOtpResponse(
    string Message,
    string? MaskedPhone = null,
    /// <summary>Only returned when SMS mock/dev mode is on — never in real SMS production.</summary>
    string? DebugOtp = null);
