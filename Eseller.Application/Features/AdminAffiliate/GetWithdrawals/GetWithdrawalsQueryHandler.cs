using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminAffiliate.GetWithdrawals;

public class GetWithdrawalsQueryHandler
    : IRequestHandler<GetWithdrawalsQuery, Result<PagedList<AdminWithdrawalDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetWithdrawalsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<AdminWithdrawalDto>>> Handle(
        GetWithdrawalsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Auth check
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<AdminWithdrawalDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        // 2. Role check — SuperAdmin/Partner only
        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<AdminWithdrawalDto>>(
                "Only admins can view withdrawals.", "FORBIDDEN");

        // 3. Query
        var query = _context.Withdrawals.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<WithdrawalStatus>(request.Status, true, out var status))
        {
            query = query.Where(w => w.Status == status);
        }

        if (request.AccountId.HasValue)
            query = query.Where(w => w.AccountId == request.AccountId.Value);

        query = query.OrderByDescending(w => w.RequestedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var withdrawals = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // 4. Load accounts + affiliates + shopkeepers + shops + bank settings
        var accountIds = withdrawals.Select(w => w.AccountId).Distinct().ToList();

        var affiliates = await _context.Affiliates
            .Where(a => accountIds.Contains(a.AccountId))
            .ToDictionaryAsync(a => a.AccountId, a => a, cancellationToken);

        var accounts = await _context.Accounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Email ?? string.Empty, cancellationToken);

        var shopkeepers = await _context.Shopkeepers
            .AsNoTracking()
            .Where(sk => accountIds.Contains(sk.AccountId))
            .ToListAsync(cancellationToken);

        var keeperIds = shopkeepers.Select(sk => sk.Id).ToList();

        var shops = await _context.Shops
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.DeletedAt == null && keeperIds.Contains(s.ShopkeeperId))
            .Select(s => new { s.Id, s.ShopkeeperId, s.Name, s.CreatedAt })
            .ToListAsync(cancellationToken);

        var shopNameByKeeper = shops
            .GroupBy(s => s.ShopkeeperId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(s => s.CreatedAt).First().Name);

        var keeperByAccountId = shopkeepers
            .GroupBy(sk => sk.AccountId)
            .ToDictionary(g => g.Key, g => g.First());

        var bankSettingKeys = accountIds.Select(id => SellerBankDetailsStore.SettingKeyFor(id)).ToList();
        var bankSettings = await _context.Settings
            .AsNoTracking()
            .Where(s => bankSettingKeys.Contains(s.Key) && s.IsActive && !string.IsNullOrWhiteSpace(s.Value))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        // 5. Build result
        var result = withdrawals.Select(w =>
        {
            var affiliate = affiliates.GetValueOrDefault(w.AccountId);
            var email = accounts.GetValueOrDefault(w.AccountId, string.Empty);
            var keeper = keeperByAccountId.GetValueOrDefault(w.AccountId);

            string? storeName = null;
            if (keeper != null)
            {
                storeName = shopNameByKeeper.GetValueOrDefault(keeper.Id, keeper.Name);
            }

            var userType = keeper != null ? "Seller" : (affiliate != null ? "Affiliate" : "Account");
            var userName = storeName ?? affiliate?.ReferralCode ?? (string.IsNullOrWhiteSpace(email) ? "Account" : email);

            var settingKey = SellerBankDetailsStore.SettingKeyFor(w.AccountId);
            var hasBankDetails = bankSettings.ContainsKey(settingKey);

            string paymentMethod = "Bank Transfer";
            string accountDetails = string.Empty;

            if (!string.IsNullOrWhiteSpace(w.PayoutMethod))
            {
                var parts = w.PayoutMethod.Split(" | ", 2, StringSplitOptions.TrimEntries);
                paymentMethod = parts[0];
                if (parts.Length > 1)
                {
                    accountDetails = parts[1];
                }
            }

            if (hasBankDetails && string.IsNullOrWhiteSpace(accountDetails))
            {
                try
                {
                    var rawJson = bankSettings[settingKey];
                    using var doc = System.Text.Json.JsonDocument.Parse(rawJson);
                    var root = doc.RootElement;
                    var bankName = root.TryGetProperty("bankName", out var bProp) ? bProp.GetString() : null;
                    var holderName = root.TryGetProperty("cardHolderName", out var hProp) ? hProp.GetString() : null;
                    var cardNum = root.TryGetProperty("cardNumber", out var cProp) ? cProp.GetString() : null;
                    var last4 = !string.IsNullOrEmpty(cardNum) && cardNum.Length >= 4 ? cardNum[^4..] : cardNum;
                    accountDetails = $"{holderName} · {bankName} · ****{last4}";
                }
                catch
                {
                    // Ignore parse fallback
                }
            }

            return new AdminWithdrawalDto(
                w.Id,
                w.AccountId,
                email,
                affiliate?.ReferralCode ?? string.Empty,
                w.Amount,
                w.PayoutMethod,
                w.Status.ToString(),
                w.RequestedAt,
                w.ProcessedAt,
                w.CreatedAt,
                userName,
                userType,
                storeName,
                paymentMethod,
                accountDetails,
                hasBankDetails);
        }).ToList();

        var pagedList = new PagedList<AdminWithdrawalDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}