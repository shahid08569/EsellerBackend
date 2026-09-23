namespace Eseller.Application.Features.AdminSettings;

public record SettingDto(
    Guid Id,
    string Key,
    string Value,
    string Type,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);