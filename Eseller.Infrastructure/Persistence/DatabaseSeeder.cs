using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eseller.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly EsellerDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTime _dateTime;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        EsellerDbContext context,
        IPasswordHasher passwordHasher,
        IDateTime dateTime,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedSuperAdminAsync(cancellationToken);
        await SeedStandardTiersAsync(cancellationToken);
        await SeedCatalogCategoriesAndBrandsAsync(cancellationToken);
    }

    /// <summary>
    /// Seed marketplace product categories + common brands when the catalog is empty.
    /// </summary>
    private async Task SeedCatalogCategoriesAndBrandsAsync(CancellationToken cancellationToken)
    {
        var now = _dateTime.UtcNow;
        var createdCats = 0;
        var createdBrands = 0;

        var categories = new[]
        {
            new { Name = "Electronics", Slug = "electronics", Order = 1 },
            new { Name = "Mobiles & Tablets", Slug = "mobiles-tablets", Order = 2 },
            new { Name = "Computers & Laptops", Slug = "computers-laptops", Order = 3 },
            new { Name = "Fashion", Slug = "fashion", Order = 4 },
            new { Name = "Home & Living", Slug = "home-living", Order = 5 },
            new { Name = "Beauty & Personal Care", Slug = "beauty-personal-care", Order = 6 },
            new { Name = "Sports & Outdoors", Slug = "sports-outdoors", Order = 7 },
            new { Name = "Groceries", Slug = "groceries", Order = 8 },
            new { Name = "Toys & Kids", Slug = "toys-kids", Order = 9 },
            new { Name = "Automotive", Slug = "automotive", Order = 10 }
        };

        foreach (var spec in categories)
        {
            var exists = await _context.Categories
                .IgnoreQueryFilters()
                .AnyAsync(c => c.Slug == spec.Slug || c.Name == spec.Name, cancellationToken);
            if (exists) continue;

            var cat = new Category
            {
                Name = spec.Name,
                Slug = spec.Slug,
                IsActive = true,
                IsFeaturedOnHomepage = spec.Order <= 4,
                HomepageDisplayOrder = spec.Order
            };
            cat.SetCreatedAt(now);
            cat.SetUpdatedAt(now);
            _context.Categories.Add(cat);
            createdCats++;
        }

        var brands = new[]
        {
            new { Name = "Samsung", Slug = "samsung" },
            new { Name = "Apple", Slug = "apple" },
            new { Name = "Xiaomi", Slug = "xiaomi" },
            new { Name = "Sony", Slug = "sony" },
            new { Name = "Huawei", Slug = "huawei" },
            new { Name = "Nike", Slug = "nike" },
            new { Name = "Adidas", Slug = "adidas" },
            new { Name = "Generic / Unbranded", Slug = "generic-unbranded" }
        };

        foreach (var spec in brands)
        {
            var exists = await _context.Brands
                .IgnoreQueryFilters()
                .AnyAsync(b => b.Slug == spec.Slug || b.Name == spec.Name, cancellationToken);
            if (exists) continue;

            var brand = new Brand
            {
                Name = spec.Name,
                Slug = spec.Slug,
                IsActive = true
            };
            brand.SetCreatedAt(now);
            brand.SetUpdatedAt(now);
            _context.Brands.Add(brand);
            createdBrands++;
        }

        if (createdCats > 0 || createdBrands > 0)
            await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Catalog seed: {Cats} categories, {Brands} brands created.",
            createdCats, createdBrands);
    }

    private async Task SeedSuperAdminAsync(CancellationToken cancellationToken)
    {
        const string superAdminEmail = "superadmin@eseller.com";
        const string superAdminUsername = "superadmin";
        const string superAdminPassword = "SuperAdmin@123"; // ⚠️ Change in production

        // Find by email OR username OR any SuperAdmin role account
        var existing = await _context.Accounts
            .FirstOrDefaultAsync(a =>
                a.Email == superAdminEmail
                || a.Username == superAdminUsername
                || a.RoleType == RoleType.SuperAdmin, cancellationToken);

        if (existing is not null)
        {
            // Always repair: reactivate + restore known local credentials
            existing.Email = superAdminEmail;
            existing.Username = superAdminUsername;
            existing.PasswordHash = _passwordHasher.Hash(superAdminPassword);
            existing.RoleType = RoleType.SuperAdmin;
            existing.IsActive = true;
            existing.IsEmailVerified = true;
            existing.IsPhoneVerified = true;

            var hasProfile = await _context.SuperAdmins
                .AnyAsync(s => s.AccountId == existing.Id, cancellationToken);
            if (!hasProfile)
            {
                _context.SuperAdmins.Add(new SuperAdmin { AccountId = existing.Id });
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "🔐 SuperAdmin repaired/reactivated — Email: {Email}, Password: {Password}",
                superAdminEmail, superAdminPassword);
            return;
        }

        var account = new Account
        {
            Username = superAdminUsername,
            Email = superAdminEmail,
            PasswordHash = _passwordHasher.Hash(superAdminPassword),
            RoleType = RoleType.SuperAdmin,
            IsActive = true,
            IsEmailVerified = true,
            IsPhoneVerified = true
        };

        _context.Accounts.Add(account);
        _context.SuperAdmins.Add(new SuperAdmin { AccountId = account.Id });
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "🔐 SuperAdmin seeded — Email: {Email}, Username: {Username}, Password: {Password}",
            superAdminEmail, superAdminUsername, superAdminPassword);
    }

    /// <summary>
    /// Ensure Bronze (200) / Gold (1000) / Diamond (5000) tiers exist.
    /// Insert-only for missing tiers; always bump Diamond capacity to 5000 if lower.
    /// </summary>
    private async Task SeedStandardTiersAsync(CancellationToken cancellationToken)
    {
        var specs = new[]
        {
            new { Name = "Bronze (Free)", Slug = "bronze-free", Price = 0m, Limit = 200, Badge = "Bronze (Free)", Color = "#94A3B8", Order = 1 },
            new { Name = "Gold", Slug = "gold", Price = 1000m, Limit = 1000, Badge = "Gold", Color = "#F59E0B", Order = 2 },
            new { Name = "Diamond", Slug = "diamond", Price = 2000m, Limit = 5000, Badge = "Diamond", Color = "#06B6D4", Order = 3 }
        };

        var created = 0;
        var now = _dateTime.UtcNow;

        foreach (var spec in specs)
        {
            var exists = await _context.ShopCategories
                .AnyAsync(c => c.DeletedAt == null && (c.Slug == spec.Slug || c.Name == spec.Name), cancellationToken);

            if (exists)
                continue;

            var tier = new ShopCategory
            {
                Name = spec.Name,
                Slug = spec.Slug,
                Description = $"{spec.Name} package — up to {spec.Limit} products.",
                BadgeText = spec.Badge,
                BadgeColor = spec.Color,
                IconUrl = "⭐",
                DisplayOrder = spec.Order,
                PriceUsd = spec.Price,
                MaxProductListings = spec.Limit,
                IsActive = true
            };
            tier.SetCreatedAt(now);
            tier.SetUpdatedAt(now);
            _context.ShopCategories.Add(tier);
            created++;
        }

        var diamond = await _context.ShopCategories
            .FirstOrDefaultAsync(c => c.DeletedAt == null && (c.Slug == "diamond" || c.Name == "Diamond"), cancellationToken);
        var diamondUpdated = false;
        if (diamond is not null && diamond.MaxProductListings < 5000)
        {
            diamond.MaxProductListings = 5000;
            if (diamond.PriceUsd <= 0) diamond.PriceUsd = 2000m;
            diamond.Description = "Diamond package — up to 5000 products.";
            diamond.SetUpdatedAt(now);
            diamondUpdated = true;
        }

        if (created > 0 || diamondUpdated)
            await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seller tiers ready: Bronze Free 200 / Gold 1000 / Diamond 5000 (created {Created}).",
            created);
    }
}