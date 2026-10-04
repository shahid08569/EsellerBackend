using Eseller.Application.Abstractions;
using Eseller.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/admin/customers")]
[Authorize(Roles = "SuperAdmin,Partner")]
public class AdminCustomersController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AdminCustomersController(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 200) pageSize = 200;
        if (pageSize < 1) pageSize = 20;
        if (pageNumber < 1) pageNumber = 1;

        // Platform shoppers only — exclude sellers/partners/admins that got a User row
        // for support-chat FK wiring (ShopSupportChannel).
        var query = from u in _context.Users
                    join a in _context.Accounts on u.AccountId equals a.Id
                    where a.RoleType == RoleType.User
                    select new
                    {
                        u.Id,
                        u.AccountId,
                        u.Name,
                        a.Email,
                        Phone = u.Phone ?? "",
                        a.IsActive,
                        ApprovalStatus = u.Status.ToString(),
                        a.LastLoginAt,
                        u.CreatedAt
                    };

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();
        var accountIds = users.Select(u => u.AccountId).ToList();

        var orders = await _context.OrderRequests
            .Where(o => userIds.Contains(o.UserId))
            .Select(o => new { o.Id, o.UserId })
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(o => o.Id).ToList();
        var items = await _context.OrderRequestItems
            .Where(i => orderIds.Contains(i.OrderRequestId))
            .Select(i => new { i.OrderRequestId, TotalPrice = i.TotalPriceSnapshot > 0 ? i.TotalPriceSnapshot : (i.Quantity * i.UnitPriceSnapshot) })
            .ToListAsync(cancellationToken);

        var spentByOrder = items.GroupBy(i => i.OrderRequestId).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalPrice));
        var statsByUser = orders.GroupBy(o => o.UserId).ToDictionary(
            g => g.Key,
            g => new
            {
                TotalOrders = g.Count(),
                TotalSpent = g.Sum(o => spentByOrder.TryGetValue(o.Id, out var sp) ? sp : 0m)
            });

        // Fetch latest location log for each customer
        var locationLogs = await _context.LocationLogs
            .Where(l => l.AccountId.HasValue && accountIds.Contains(l.AccountId.Value))
            .OrderByDescending(l => l.RecordedAt)
            .ToListAsync(cancellationToken);

        var latestLogByAccount = locationLogs
            .GroupBy(l => l.AccountId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var resultItems = users.Select(u =>
        {
            latestLogByAccount.TryGetValue(u.AccountId, out var loc);
            return new
            {
                u.Id,
                u.AccountId,
                u.Name,
                u.Email,
                u.Phone,
                TotalOrders = statsByUser.TryGetValue(u.Id, out var st) ? st.TotalOrders : 0,
                TotalSpent = statsByUser.TryGetValue(u.Id, out var st2) ? st2.TotalSpent : 0m,
                u.IsActive,
                status = u.ApprovalStatus,
                u.CreatedAt,
                u.LastLoginAt,
                LastLoginCity = loc?.City,
                LastLoginCountry = loc?.Country,
                LastLoginDevice = loc?.DeviceType,
                Latitude = loc?.Latitude,
                Longitude = loc?.Longitude
            };
        }).ToList();

        return Ok(new
        {
            items = resultItems,
            totalCount,
            pageNumber,
            pageSize
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCustomerDetails(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null) return NotFound(new { error = "Customer not found." });

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == user.AccountId, cancellationToken);
        if (account is null || account.RoleType != RoleType.User)
            return NotFound(new { error = "Customer not found." });

        var addresses = await _context.Addresses
            .Where(a => a.UserId == user.Id && a.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var recentOrders = await _context.OrderRequests
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .Select(o => new
            {
                o.Id,
                o.Status,
                o.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var locationHistory = await _context.LocationLogs
            .Where(l => l.AccountId == user.AccountId)
            .OrderByDescending(l => l.RecordedAt)
            .Take(30)
            .Select(l => new
            {
                l.Id,
                l.City,
                l.Country,
                l.CountryCode,
                l.DeviceType,
                l.IpAddress,
                l.Latitude,
                l.Longitude,
                l.Action,
                l.RecordedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            id = user.Id,
            accountId = user.AccountId,
            name = user.Name,
            email = account?.Email ?? "",
            phone = user.Phone,
            isActive = account?.IsActive ?? true,
            status = user.Status.ToString(),
            rejectionReason = user.RejectionReason,
            approvedAt = user.ApprovedAt,
            createdAt = user.CreatedAt,
            lastLoginAt = account?.LastLoginAt,
            addresses,
            recentOrders,
            locationHistory
        });
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveCustomer(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null) return NotFound(new { error = "Customer not found." });

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == user.AccountId, cancellationToken);
        if (account is null || account.RoleType != RoleType.User)
            return NotFound(new { error = "Customer not found." });

        if (user.Status != UserApprovalStatus.Pending)
            return BadRequest(new { error = $"Customer is already {user.Status}.", errorCode = "INVALID_STATUS" });

        user.Status = UserApprovalStatus.Approved;
        user.ApprovedAt = DateTime.UtcNow;
        user.RejectionReason = null;
        user.ApprovedByAccountId = _currentUser.AccountId;

        account.IsActive = true;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Customer approved. They can now place orders." });
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectCustomer(
        Guid id,
        [FromBody] RejectCustomerRequest? request,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null) return NotFound(new { error = "Customer not found." });

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == user.AccountId, cancellationToken);
        if (account is null || account.RoleType != RoleType.User)
            return NotFound(new { error = "Customer not found." });

        if (user.Status != UserApprovalStatus.Pending)
            return BadRequest(new { error = $"Customer is already {user.Status}.", errorCode = "INVALID_STATUS" });

        user.Status = UserApprovalStatus.Rejected;
        user.ApprovedAt = DateTime.UtcNow;
        user.RejectionReason = string.IsNullOrWhiteSpace(request?.Reason)
            ? "Rejected by Super Admin."
            : request!.Reason.Trim();

        account.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Customer registration rejected." });
    }

    [HttpGet("{id:guid}/logins")]
    public async Task<IActionResult> GetCustomerLogins(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null) return NotFound(new { error = "Customer not found." });

        var logs = await _context.LocationLogs
            .Where(l => l.AccountId == user.AccountId)
            .OrderByDescending(l => l.RecordedAt)
            .Take(100)
            .Select(l => new
            {
                l.Id,
                l.City,
                l.Country,
                l.CountryCode,
                l.DeviceType,
                l.IpAddress,
                l.Latitude,
                l.Longitude,
                l.Action,
                l.RecordedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(logs);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(
        Guid id,
        [FromBody] SetCustomerStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null) return NotFound(new { error = "Customer not found." });

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == user.AccountId, cancellationToken);
        if (account != null)
        {
            account.IsActive = request.IsActive;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message = $"Customer status updated to {(request.IsActive ? "Active" : "Suspended")}." });
    }
}

public record SetCustomerStatusRequest(bool IsActive);

public record RejectCustomerRequest(string? Reason);
