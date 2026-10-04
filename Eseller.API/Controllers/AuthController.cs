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
    private const string RefreshTokenCookieName = "refreshToken";

    private readonly ISender _sender;
    private readonly JwtSettings _jwtSettings;

    public AuthController(ISender sender, IOptions<JwtSettings> jwtSettings)
    {
        _sender = sender;
        _jwtSettings = jwtSettings.Value;
    }

    // ═══════════════════════════════════════════════════════════
    // REGISTER — CUSTOMER
    // ═══════════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════
    // REGISTER — SELLER
    // ═══════════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════
    // VERIFY EMAIL
    // ═══════════════════════════════════════════════════════════
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        return Ok(new { message = "Email verified successfully." });
    }

    // ═══════════════════════════════════════════════════════════
    // RESEND VERIFICATION EMAIL
    // ═══════════════════════════════════════════════════════════
    [HttpPost("resend-verification-email")]
    public async Task<IActionResult> ResendVerificationEmail(
        [FromBody] ResendVerificationEmailCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        return Ok(new { message = "Verification email sent." });
    }

    // ═══════════════════════════════════════════════════════════
    // LOGIN — Sets refresh token as HttpOnly cookie
    // ═══════════════════════════════════════════════════════════
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

        // Set refresh token as HttpOnly cookie
        SetRefreshTokenCookie(result.Value!.RefreshToken);

        // Return only safe data (no refreshToken in JSON)
        return Ok(new
        {
            accountId = result.Value.AccountId,
            username = result.Value.Username,
            email = result.Value.Email,
            roleType = result.Value.RoleType,
            accessToken = result.Value.AccessToken,
            accessTokenExpiresAt = result.Value.AccessTokenExpiresAt
        });
    }

    // ═══════════════════════════════════════════════════════════
    // REFRESH TOKEN — Reads from HttpOnly cookie
    // ═══════════════════════════════════════════════════════════
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new
            {
                error = "Refresh token is missing.",
                errorCode = "REFRESH_TOKEN_MISSING"
            });
        }

        var command = new RefreshTokenCommand(refreshToken);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            Response.Cookies.Delete(RefreshTokenCookieName);

            return Unauthorized(new
            {
                error = result.Error,
                errorCode = result.ErrorCode
            });
        }

        // ✅ Rotate refresh token cookie
        SetRefreshTokenCookie(result.Value!.RefreshToken);

        // Return account info so frontend can restore full auth state on reload
        return Ok(new
        {
            accessToken = result.Value.AccessToken,
            accessTokenExpiresAt = result.Value.AccessTokenExpiresAt,
            accountId = result.Value.AccountId,
            username = result.Value.Username,
            email = result.Value.Email,
            roleType = result.Value.RoleType
        });
    }

    // ═══════════════════════════════════════════════════════════
    // LOGOUT — Deletes cookie + revokes server-side
    // ═══════════════════════════════════════════════════════════
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];

        Response.Cookies.Delete(RefreshTokenCookieName);

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Ok(new { message = "Logged out successfully." });
        }

        var command = new LogoutCommand(refreshToken);
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

    // ═══════════════════════════════════════════════════════════
    // CHANGE PASSWORD
    // ═══════════════════════════════════════════════════════════
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordCommand command,
        CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        return Ok(new { message = "Password changed successfully. Please login again." });
    }

    // ═══════════════════════════════════════════════════════════
    // SEND PHONE OTP
    // ═══════════════════════════════════════════════════════════
    [HttpPost("send-phone-otp")]
    public async Task<IActionResult> SendPhoneOtp(
        [FromBody] SendPhoneOtpCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        return Ok(new
        {
            message = result.Value!.Message,
            maskedPhone = result.Value.MaskedPhone,
            debugOtp = result.Value.DebugOtp
        });
    }

    // ═══════════════════════════════════════════════════════════
    // VERIFY PHONE OTP
    // ═══════════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════
    // FORGOT PASSWORD
    // ═══════════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════
    // RESET PASSWORD
    // ═══════════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════
    // UPLOAD VERIFICATION DOCUMENT
    // ═══════════════════════════════════════════════════════════
    [HttpPost("upload-document")]
    [AllowAnonymous]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> UploadDocument(
        IFormFile file,
        [FromServices] IFileStorageService fileStorageService,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Document file is required.", errorCode = "FILE_REQUIRED" });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest(new { error = "Only JPG, PNG, PDF, and WEBP documents are allowed.", errorCode = "INVALID_FILE_TYPE" });

        var uniqueFileName = $"documents/verification-{Guid.NewGuid()}{ext}";
        using var stream = file.OpenReadStream();
        var documentUrl = await fileStorageService.UploadAsync(
            stream,
            uniqueFileName,
            file.ContentType,
            cancellationToken);

        return Ok(new { documentUrl, fileName = file.FileName });
    }

    // ═══════════════════════════════════════════════════════════
    // HELPER — Set HttpOnly Cookie
    // ═══════════════════════════════════════════════════════════
    private void SetRefreshTokenCookie(string refreshToken)
    {
        var isProduction = !Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays)
        };

        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, cookieOptions);
    }
}