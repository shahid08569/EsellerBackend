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
using Eseller.Domain.Enums;
using Eseller.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";

    private readonly ISender _sender;
    private readonly JwtSettings _jwtSettings;
    private readonly IHostEnvironment _env;

    public AuthController(
        ISender sender,
        IOptions<JwtSettings> jwtSettings,
        IHostEnvironment env)
    {
        _sender = sender;
        _jwtSettings = jwtSettings.Value;
        _env = env;
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
            accessTokenExpiresAt = result.Value.AccessTokenExpiresAt,
            permissions = result.Value.Permissions
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
            DeleteRefreshTokenCookie();

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
            roleType = result.Value.RoleType,
            permissions = result.Value.Permissions
        });
    }

    // ═══════════════════════════════════════════════════════════
    // LOGOUT — Deletes cookie + revokes server-side
    // ═══════════════════════════════════════════════════════════
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];

        DeleteRefreshTokenCookie();

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
    // CURRENT USER PROFILE
    // ═══════════════════════════════════════════════════════════
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentProfile(
        [FromServices] IApplicationDbContext context,
        [FromServices] ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        if (currentUserService.AccountId is null)
            return Unauthorized(new { error = "User is not authenticated.", errorCode = "UNAUTHENTICATED" });

        var account = await context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == currentUserService.AccountId.Value, cancellationToken);

        if (account is null)
            return NotFound(new { error = "Account not found.", errorCode = "ACCOUNT_NOT_FOUND" });

        string? permissions = null;
        if (account.RoleType == RoleType.SuperAdmin)
        {
            permissions = "[\"*\"]";
        }
        else if (account.RoleType == RoleType.Partner)
        {
            var partner = await context.Partners
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.AccountId == account.Id, cancellationToken);
            permissions = partner?.Permissions;
        }

        return Ok(new
        {
            id = account.Id,
            accountId = account.Id,
            username = account.Username,
            email = account.Email,
            roleType = account.RoleType.ToString(),
            permissions,
            createdAt = account.CreatedAt
        });
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateAccountNameRequest request,
        [FromServices] IApplicationDbContext context,
        [FromServices] ICurrentUserService currentUserService,
        [FromServices] IDateTime dateTime,
        CancellationToken cancellationToken)
    {
        if (currentUserService.AccountId is null)
            return Unauthorized(new { error = "User is not authenticated.", errorCode = "UNAUTHENTICATED" });

        var account = await context.Accounts
            .FirstOrDefaultAsync(a => a.Id == currentUserService.AccountId.Value, cancellationToken);

        if (account is null)
            return NotFound(new { error = "Account not found.", errorCode = "ACCOUNT_NOT_FOUND" });

        var newName = request.Username ?? request.Name;
        if (!string.IsNullOrWhiteSpace(newName))
        {
            account.Username = newName.Trim();
        }

        account.SetUpdatedAt(dateTime.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Profile name updated successfully.",
            username = account.Username,
            email = account.Email
        });
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

        // Never expose OTP outside Development (even when mock SMS is on).
        return Ok(new
        {
            message = result.Value!.Message,
            maskedPhone = result.Value.MaskedPhone,
            debugOtp = _env.IsDevelopment() ? result.Value.DebugOtp : null
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
        var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp", "application/pdf"
        };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest(new { error = "Only JPG, PNG, PDF, and WEBP documents are allowed.", errorCode = "INVALID_FILE_TYPE" });

        if (!string.IsNullOrWhiteSpace(file.ContentType) && !allowedContentTypes.Contains(file.ContentType))
            return BadRequest(new { error = "Invalid document content type.", errorCode = "INVALID_CONTENT_TYPE" });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "Document must be 10 MB or smaller.", errorCode = "FILE_TOO_LARGE" });

        var uniqueFileName = $"documents/verification-{Guid.NewGuid()}{ext}";
        using var stream = file.OpenReadStream();
        var documentUrl = await fileStorageService.UploadAsync(
            stream,
            uniqueFileName,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            cancellationToken);

        return Ok(new { documentUrl, fileName = Path.GetFileName(file.FileName) });
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
            SameSite = isProduction ? SameSiteMode.None : SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            Path = "/",
            IsEssential = true
        };

        if (isProduction && Request.Host.Host.EndsWith("esellerglobal.com", StringComparison.OrdinalIgnoreCase))
        {
            cookieOptions.Domain = ".esellerglobal.com";
        }

        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, cookieOptions);
    }

    private void DeleteRefreshTokenCookie()
    {
        var isProduction = !Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction,
            SameSite = isProduction ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/"
        };

        if (isProduction && Request.Host.Host.EndsWith("esellerglobal.com", StringComparison.OrdinalIgnoreCase))
        {
            cookieOptions.Domain = ".esellerglobal.com";
        }

        Response.Cookies.Delete(RefreshTokenCookieName, cookieOptions);
    }
}

public record UpdateAccountNameRequest(string? Username, string? Name = null);