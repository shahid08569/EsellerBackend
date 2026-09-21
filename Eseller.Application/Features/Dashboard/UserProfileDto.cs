namespace Eseller.Application.Features.Dashboard;

public record UserProfileDto(
    Guid AccountId,
    Guid UserId,
    string Name,
    string Email,
    string? Phone,
    bool IsEmailVerified,
    bool IsPhoneVerified,
    DateTime? LastLoginAt,
    DateTime CreatedAt);