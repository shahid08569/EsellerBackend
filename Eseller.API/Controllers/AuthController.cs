using Eseller.Application.Abstractions;
using Eseller.Application.Features.Auth.ChangePassword;
using Eseller.Application.Features.Auth.ForgotPassword;
using Eseller.Application.Features.Auth.Login;
using Eseller.Application.Features.Auth.Logout;
using Eseller.Application.Features.Auth.RefreshToken;
using Eseller.Application.Features.Auth.RegisterCustomer;
using Eseller.Application.Features.Auth.RegisterSeller;
using Eseller.Application.Features.Auth.ResendVerificationEmail;
using Eseller.Application.Features.Auth.ResetPassword;
using Eseller.Application.Features.Auth.SendPhoneOtp;
using Eseller.Application.Features.Auth.VerifyEmail;
using Eseller.Application.Features.Auth.VerifyPhoneOtp;
using Eseller.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("register/customer")]
    public async Task<IActionResult> RegisterCustomer(
    [FromBody] RegisterCustomerCommand command,
    CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        return Ok(result.Value);
    }

    [HttpPost("register/seller")]
    public async Task<IActionResult> RegisterSeller(
        [FromBody] RegisterSellerCommand command,
        CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        return Ok(result.Value);
    }
  
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { 
                error = result.Error, errorCode = result.ErrorCode
            });
        return Ok(new { message = "Email verified successfully." });
    }

    [HttpPost("resend-verification-email")]
    public async Task<IActionResult> ResendVerificationEmail(
        [FromBody] ResendVerificationEmailCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { 
                error = result.Error, errorCode = result.ErrorCode
            });
        return Ok(new { message = "Verification email sent." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Unauthorized(new
            {
                error = result.Error,
                errorCode = result.ErrorCode
            });
        }

        return Ok(result.Value);
    }


    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(
       [FromBody] RefreshTokenCommand command,
       CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Unauthorized(new
            {
                error = result.Error,
                errorCode = result.ErrorCode
            });
        }

        return Ok(result.Value);
    }


    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new
            {
                error = result.Error,
                errorCode = result.ErrorCode
            });
        }

        return Ok(new { message = "Logged out successfully." });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        return Ok(new { message = "Password changed successfully. Please login again." });
    }

    [HttpPost("send-phone-otp")]
    public async Task<IActionResult> SendPhoneOtp(
    [FromBody] SendPhoneOtpCommand command,
    CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        return Ok(new { message = "OTP sent successfully." });
    }

    [HttpPost("verify-phone-otp")]
    public async Task<IActionResult> VerifyPhoneOtp(
        [FromBody] VerifyPhoneOtpCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        return Ok(new { message = "Phone verified successfully." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
    [FromBody] ForgotPasswordCommand command,
    CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        return Ok(new { message = "If the email exists, a reset code has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        return Ok(new { message = "Password reset successfully. Please login again." });
    }
}