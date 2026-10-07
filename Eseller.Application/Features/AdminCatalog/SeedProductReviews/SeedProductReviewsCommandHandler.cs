using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Engagement;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminCatalog.SeedProductReviews;

public class SeedProductReviewsCommandHandler
    : IRequestHandler<SeedProductReviewsCommand, Result<int>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTime _dateTime;

    public SeedProductReviewsCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPasswordHasher passwordHasher,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
        _dateTime = dateTime;
    }

    public async Task<Result<int>> Handle(
        SeedProductReviewsCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.RoleType is not (RoleType.SuperAdmin or RoleType.Partner))
            return Result.Failure<int>("Only Super Admin can seed reviews.", "FORBIDDEN");

        if (request.Reviews is null || request.Reviews.Count == 0)
            return Result.Failure<int>("No reviews provided.", "VALIDATION_ERROR");

        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null, cancellationToken);

        if (product is null)
            return Result.Failure<int>("Product not found.", "PRODUCT_NOT_FOUND");

        var created = 0;
        var now = _dateTime.UtcNow;

        foreach (var item in request.Reviews)
        {
            var name = (item.ReviewerName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var rating = item.Rating;
            if (rating < 1 || rating > 5)
                continue;

            var user = await GetOrCreateReviewerAsync(name, cancellationToken);

            var already = await _context.Reviews
                .AnyAsync(r => r.UserId == user.Id && r.ProductId == product.Id, cancellationToken);
            if (already)
                continue;

            _context.Reviews.Add(new Review
            {
                ProductId = product.Id,
                UserId = user.Id,
                OrderRequestItemId = null,
                Rating = rating,
                Title = string.IsNullOrWhiteSpace(item.Title) ? null : item.Title.Trim(),
                Comment = string.IsNullOrWhiteSpace(item.Comment) ? null : item.Comment.Trim(),
                IsApproved = true,
                CreatedAt = now
            });
            created++;
        }

        if (created > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);

            var approved = await _context.Reviews
                .Where(r => r.ProductId == product.Id && r.IsApproved)
                .Select(r => r.Rating)
                .ToListAsync(cancellationToken);

            product.AvgRating = approved.Count == 0
                ? null
                : (decimal)approved.Average();
            product.SetUpdatedAt(now);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(created);
    }

    private async Task<User> GetOrCreateReviewerAsync(string name, CancellationToken cancellationToken)
    {
        var slug = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", ".")
            .Trim('.');
        if (string.IsNullOrWhiteSpace(slug))
            slug = "reviewer";

        var email = $"import.{slug}@eseller.local";
        if (email.Length > 180)
            email = $"import.{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..16].ToLowerInvariant()}@eseller.local";

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == email, cancellationToken);

        if (account is not null)
        {
            var existing = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == account.Id, cancellationToken);
            if (existing is not null)
                return existing;
        }

        account = new Account
        {
            Username = email,
            Email = email,
            PasswordHash = _passwordHasher.Hash("ImportReviewer@@00"),
            RoleType = RoleType.User,
            IsActive = true,
            IsEmailVerified = true,
            IsPhoneVerified = false
        };
        _context.Accounts.Add(account);

        var user = new User
        {
            AccountId = account.Id,
            Name = name,
            Phone = null,
            Status = UserApprovalStatus.Approved,
            ApprovedAt = _dateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }
}
