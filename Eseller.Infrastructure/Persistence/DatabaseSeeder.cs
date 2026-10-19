using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using Eseller.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eseller.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly EsellerDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTime _dateTime;
    private readonly ILogger<DatabaseSeeder> _logger;
    private readonly SeedSettings _seedSettings;

    public DatabaseSeeder(
        EsellerDbContext context,
        IPasswordHasher passwordHasher,
        IDateTime dateTime,
        ILogger<DatabaseSeeder> logger,
        IOptions<SeedSettings> seedSettings)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _dateTime = dateTime;
        _logger = logger;
        _seedSettings = seedSettings.Value;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedSuperAdminAsync(cancellationToken);
        await SeedPartnerAsync(cancellationToken);
        await SeedStandardTiersAsync(cancellationToken);
        await SeedCatalogCategoriesAndBrandsAsync(cancellationToken);
        await EnsureCatalogMediaAsync(cancellationToken);
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
            new { Name = "Electronics", Slug = "electronics", Order = 1, Image = CatalogMedia.CategoryImage("electronics") },
            new { Name = "Mobiles & Tablets", Slug = "mobiles-tablets", Order = 2, Image = CatalogMedia.CategoryImage("mobiles-tablets") },
            new { Name = "Computers & Laptops", Slug = "computers-laptops", Order = 3, Image = CatalogMedia.CategoryImage("computers-laptops") },
            new { Name = "Fashion", Slug = "fashion", Order = 4, Image = CatalogMedia.CategoryImage("fashion") },
            new { Name = "Home & Living", Slug = "home-living", Order = 5, Image = CatalogMedia.CategoryImage("home-living") },
            new { Name = "Beauty & Personal Care", Slug = "beauty-personal-care", Order = 6, Image = CatalogMedia.CategoryImage("beauty-personal-care") },
            new { Name = "Sports & Outdoors", Slug = "sports-outdoors", Order = 7, Image = CatalogMedia.CategoryImage("sports-outdoors") },
            new { Name = "Groceries", Slug = "groceries", Order = 8, Image = CatalogMedia.CategoryImage("groceries") },
            new { Name = "Toys & Kids", Slug = "toys-kids", Order = 9, Image = CatalogMedia.CategoryImage("toys-kids") },
            new { Name = "Automotive", Slug = "automotive", Order = 10, Image = CatalogMedia.CategoryImage("automotive") }
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
                ImageUrl = spec.Image,
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
            new { Name = "Samsung", Slug = "samsung", Logo = CatalogMedia.BrandLogo("samsung") },
            new { Name = "Apple", Slug = "apple", Logo = CatalogMedia.BrandLogo("apple") },
            new { Name = "Xiaomi", Slug = "xiaomi", Logo = CatalogMedia.BrandLogo("xiaomi") },
            new { Name = "Sony", Slug = "sony", Logo = CatalogMedia.BrandLogo("sony") },
            new { Name = "Huawei", Slug = "huawei", Logo = CatalogMedia.BrandLogo("huawei") },
            new { Name = "Nike", Slug = "nike", Logo = CatalogMedia.BrandLogo("nike") },
            new { Name = "Adidas", Slug = "adidas", Logo = CatalogMedia.BrandLogo("adidas") },
            new { Name = "Generic / Unbranded", Slug = "generic-unbranded", Logo = CatalogMedia.BrandLogo("generic-unbranded") }
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
                LogoUrl = spec.Logo,
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

    /// <summary>
    /// Backfill missing category images / brand logos for existing rows (idempotent).
    /// </summary>
    private async Task EnsureCatalogMediaAsync(CancellationToken cancellationToken)
    {
        var now = _dateTime.UtcNow;
        var updated = 0;

        var categories = await _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.DeletedAt == null && (c.ImageUrl == null || c.ImageUrl == ""))
            .ToListAsync(cancellationToken);

        foreach (var cat in categories)
        {
            var url = CatalogMedia.CategoryImage(cat.Slug);
            if (string.IsNullOrWhiteSpace(url)) continue;
            cat.ImageUrl = url;
            cat.SetUpdatedAt(now);
            updated++;
        }

        // Fill missing brand logos; also replace stale local /uploads logos for known brands
        var brands = await _context.Brands
            .IgnoreQueryFilters()
            .Where(b => b.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var brand in brands)
        {
            var url = CatalogMedia.BrandLogo(brand.Slug);
            if (string.IsNullOrWhiteSpace(url)) continue;

            var current = brand.LogoUrl?.Trim() ?? "";
            var needsFill = string.IsNullOrWhiteSpace(current)
                || current.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
                || current.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase);
            if (!needsFill) continue;

            brand.LogoUrl = url;
            brand.SetUpdatedAt(now);
            updated++;
        }

        if (updated > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Catalog media backfill: {Count} rows updated.", updated);
        }
    }

    private async Task SeedSuperAdminAsync(CancellationToken cancellationToken)
    {
        var email = string.IsNullOrWhiteSpace(_seedSettings.SuperAdminEmail)
            ? "superadmin@eseller.com"
            : _seedSettings.SuperAdminEmail.Trim();
        var username = string.IsNullOrWhiteSpace(_seedSettings.SuperAdminUsername)
            ? "superadmin"
            : _seedSettings.SuperAdminUsername.Trim();

        var existingAdmin = await _context.Accounts
            .AnyAsync(a => a.RoleType == RoleType.SuperAdmin, cancellationToken);

        if (existingAdmin)
        {
            // Never reset credentials of an existing SuperAdmin (production-safe)
            return;
        }

        var password = _seedSettings.SuperAdminPassword?.Trim() ?? string.Empty;
        if (password.Length < 12)
        {
            _logger.LogWarning(
                "SuperAdmin seed skipped — set SeedSettings:SuperAdminPassword (min 12 chars) via user-secrets or environment.");
            return;
        }

        var account = new Account
        {
            Username = username,
            Email = email,
            PasswordHash = _passwordHasher.Hash(password),
            RoleType = RoleType.SuperAdmin,
            IsActive = true,
            IsEmailVerified = true,
            IsPhoneVerified = true
        };

        _context.Accounts.Add(account);
        _context.SuperAdmins.Add(new SuperAdmin { AccountId = account.Id });
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "SuperAdmin account created for {Email}. Change the password after first login.",
            email);
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

    private async Task SeedPartnerAsync(CancellationToken cancellationToken)
    {
        const string partnerEmail = "tomadmin@gmail.com";
        const string partnerPassword = "Tom@1234";

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == partnerEmail, cancellationToken);

        var now = _dateTime.UtcNow;
        if (account is null)
        {
            account = new Account
            {
                Username = partnerEmail,
                Email = partnerEmail,
                PasswordHash = _passwordHasher.Hash(partnerPassword),
                RoleType = RoleType.Partner,
                IsActive = true,
                IsEmailVerified = true,
                IsPhoneVerified = true
            };
            account.SetCreatedAt(now);
            account.SetUpdatedAt(now);
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            account.RoleType = RoleType.Partner;
            account.PasswordHash = _passwordHasher.Hash(partnerPassword);
            account.IsActive = true;
            account.SetUpdatedAt(now);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var partner = await _context.Partners
            .FirstOrDefaultAsync(p => p.AccountId == account.Id, cancellationToken);

        const string allPermissions = "shops:view,shops:manage,products:view,products:manage,catalog:manage,orders:view,orders:manage,orders:place,customers:manage,finance:view,finance:manage,traffic:view,traffic:manage";

        if (partner is null)
        {
            partner = new Partner
            {
                AccountId = account.Id,
                AddedBySuperAdminId = Guid.Empty,
                AddedAt = now,
                Permissions = allPermissions,
                ReferralCode = "PRT-TOMADMIN"
            };
            _context.Partners.Add(partner);
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            partner.Permissions = allPermissions;
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Partner account seeded/updated for {Email}", partnerEmail);
    }
}