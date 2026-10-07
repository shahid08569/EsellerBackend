using System.Text.Json;
using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Content;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Common;

public record SellerWithdrawalPaymentMethodDto(
    string Id,
    string Name,
    string Details,
    bool IsActive);

public record UpsertSellerWithdrawalPaymentMethodRequest(
    string Name,
    string Details,
    bool IsActive = true);

public static class SellerWithdrawalPaymentMethodsStore
{
    public const string SettingKey = "SellerWithdrawalPaymentMethods";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly Guid DefaultJazzCashId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid DefaultBankTransferId = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901");

    public static IReadOnlyList<SellerWithdrawalPaymentMethodDto> GetDefaultMethods() =>
    [
        new(DefaultJazzCashId.ToString(), "JazzCash", "Account: 0300-1234567", true),
        new(
            DefaultBankTransferId.ToString(),
            "Bank Transfer",
            "HBL — Account Title: Eseller Payouts, IBAN: PK00HABB0000001234567890",
            true)
    ];

    public static async Task<IReadOnlyList<SellerWithdrawalPaymentMethodDto>> LoadAllAsync(
        IApplicationDbContext context,
        IDateTime dateTime,
        bool seedIfEmpty,
        CancellationToken cancellationToken)
    {
        var setting = await context.Settings
            .FirstOrDefaultAsync(s => s.Key == SettingKey, cancellationToken);

        var methods = ParseMethods(setting?.Value);
        if (methods.Count > 0 || !seedIfEmpty)
            return methods;

        var defaults = GetDefaultMethods().ToList();
        await PersistAsync(context, dateTime, setting, defaults, cancellationToken);
        return defaults;
    }

    public static async Task SaveAllAsync(
        IApplicationDbContext context,
        IDateTime dateTime,
        IReadOnlyList<SellerWithdrawalPaymentMethodDto> methods,
        CancellationToken cancellationToken)
    {
        var setting = await context.Settings
            .FirstOrDefaultAsync(s => s.Key == SettingKey, cancellationToken);

        await PersistAsync(context, dateTime, setting, methods.ToList(), cancellationToken);
    }

    private static async Task PersistAsync(
        IApplicationDbContext context,
        IDateTime dateTime,
        Setting? setting,
        List<SellerWithdrawalPaymentMethodDto> methods,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(methods, JsonOptions);
        var now = dateTime.UtcNow;

        if (setting is null)
        {
            setting = new Setting
            {
                Key = SettingKey,
                Value = json,
                Type = "json",
                IsActive = true
            };
            setting.SetCreatedAt(now);
            setting.SetUpdatedAt(now);
            context.Settings.Add(setting);
        }
        else
        {
            setting.Value = json;
            setting.Type = "json";
            setting.IsActive = true;
            setting.SetUpdatedAt(now);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static List<SellerWithdrawalPaymentMethodDto> ParseMethods(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var parsed = JsonSerializer.Deserialize<List<SellerWithdrawalPaymentMethodDto>>(json, JsonOptions);
            return parsed?
                       .Where(m => !string.IsNullOrWhiteSpace(m.Id) && !string.IsNullOrWhiteSpace(m.Name))
                       .ToList()
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
