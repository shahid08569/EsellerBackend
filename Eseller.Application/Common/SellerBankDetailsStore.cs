using System.Text.Json;
using System.Text.RegularExpressions;
using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Content;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Common;

/// <summary>Seller-visible bank/card details (masked; CVC never returned after save).</summary>
public record SellerBankDetailsDto(
    string BankName,
    string CardHolderName,
    string CardNumberLast4,
    string CardNumberMasked,
    int ExpMonth,
    int ExpYear,
    bool HasCvcSaved,
    DateTime UpdatedAt);

/// <summary>Super Admin payout view — includes full PAN and CVC stored for manual payout processing.</summary>
public record AdminSellerBankDetailsDto(
    string BankName,
    string CardHolderName,
    string CardNumberLast4,
    string CardNumberMasked,
    int ExpMonth,
    int ExpYear,
    bool HasCvcSaved,
    DateTime UpdatedAt,
    string CardNumberFull,
    string? Cvc);

public record UpsertSellerBankDetailsRequest(
    string BankName,
    string CardHolderName,
    string CardNumber,
    int ExpMonth,
    int ExpYear,
    string Cvc);

internal sealed class SellerBankDetailsPayload
{
    public string BankName { get; set; } = string.Empty;
    public string CardHolderName { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string? Cvc { get; set; }
    public int ExpMonth { get; set; }
    public int ExpYear { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class SellerBankDetailsStore
{
    public const string SettingKeyPrefix = "SellerBankDetails:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string SettingKeyFor(Guid accountId) => $"{SettingKeyPrefix}{accountId:N}";

    public static async Task<SellerBankDetailsDto?> LoadForSellerAsync(
        IApplicationDbContext context,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var payload = await LoadPayloadAsync(context, accountId, cancellationToken);
        return payload is null ? null : ToSellerDto(payload);
    }

    public static async Task<AdminSellerBankDetailsDto?> LoadForAdminAsync(
        IApplicationDbContext context,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var payload = await LoadPayloadAsync(context, accountId, cancellationToken);
        return payload is null ? null : ToAdminDto(payload);
    }

    public static async Task<SellerBankDetailsDto> SaveAsync(
        IApplicationDbContext context,
        IDateTime dateTime,
        Guid accountId,
        UpsertSellerBankDetailsRequest request,
        CancellationToken cancellationToken)
    {
        var digits = DigitsOnly(request.CardNumber);
        var cvcDigits = DigitsOnly(request.Cvc);
        var now = dateTime.UtcNow;

        var payload = new SellerBankDetailsPayload
        {
            BankName = request.BankName.Trim(),
            CardHolderName = request.CardHolderName.Trim(),
            CardNumber = digits,
            Cvc = cvcDigits,
            ExpMonth = request.ExpMonth,
            ExpYear = request.ExpYear,
            UpdatedAt = now
        };

        var key = SettingKeyFor(accountId);
        var setting = await context.Settings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        var json = JsonSerializer.Serialize(payload, JsonOptions);

        if (setting is null)
        {
            setting = new Setting
            {
                Key = key,
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
        return ToSellerDto(payload);
    }

    public static string? ValidateUpsert(UpsertSellerBankDetailsRequest request, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(request.BankName))
            return "Bank name is required.";

        if (string.IsNullOrWhiteSpace(request.CardHolderName))
            return "Card holder name is required.";

        var cardDigits = DigitsOnly(request.CardNumber);
        if (cardDigits.Length is < 13 or > 19)
            return "Card number must be 13–19 digits.";

        if (request.ExpMonth is < 1 or > 12)
            return "Expiration month must be between 1 and 12.";

        var cvcDigits = DigitsOnly(request.Cvc);
        if (cvcDigits.Length is < 3 or > 4)
            return "CVC must be 3 or 4 digits.";

        var currentYear = utcNow.Year;
        var currentMonth = utcNow.Month;
        if (request.ExpYear < currentYear
            || (request.ExpYear == currentYear && request.ExpMonth < currentMonth))
            return "Card expiration date must not be in the past.";

        return null;
    }

    private static async Task<SellerBankDetailsPayload?> LoadPayloadAsync(
        IApplicationDbContext context,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var key = SettingKeyFor(accountId);
        var setting = await context.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key && s.IsActive, cancellationToken);

        if (setting is null || string.IsNullOrWhiteSpace(setting.Value))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SellerBankDetailsPayload>(setting.Value, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SellerBankDetailsDto ToSellerDto(SellerBankDetailsPayload payload)
    {
        var last4 = LastFour(payload.CardNumber);
        return new SellerBankDetailsDto(
            payload.BankName,
            payload.CardHolderName,
            last4,
            MaskCardNumber(payload.CardNumber),
            payload.ExpMonth,
            payload.ExpYear,
            !string.IsNullOrEmpty(payload.Cvc),
            payload.UpdatedAt);
    }

    private static AdminSellerBankDetailsDto ToAdminDto(SellerBankDetailsPayload payload)
    {
        var last4 = LastFour(payload.CardNumber);
        return new AdminSellerBankDetailsDto(
            payload.BankName,
            payload.CardHolderName,
            last4,
            MaskCardNumber(payload.CardNumber),
            payload.ExpMonth,
            payload.ExpYear,
            !string.IsNullOrEmpty(payload.Cvc),
            payload.UpdatedAt,
            payload.CardNumber,
            payload.Cvc);
    }

    private static string DigitsOnly(string value) =>
        Regex.Replace(value ?? string.Empty, @"\D", string.Empty);

    private static string LastFour(string cardNumber)
    {
        var digits = DigitsOnly(cardNumber);
        return digits.Length >= 4 ? digits[^4..] : digits;
    }

    private static string MaskCardNumber(string cardNumber)
    {
        var digits = DigitsOnly(cardNumber);
        if (digits.Length == 0)
            return string.Empty;

        var last4 = digits.Length >= 4 ? digits[^4..] : digits;
        return $"**** **** **** {last4}";
    }
}
