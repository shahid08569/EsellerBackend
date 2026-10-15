using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Eseller.Application.Abstractions;
using Eseller.Domain.Enums;
using Eseller.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/seed-management")]
public class SeedManagementController : ControllerBase
{
    private const string SecretAuthKey = "Eseller2026SecureSeedKey!";
    private readonly EsellerDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public SeedManagementController(EsellerDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    private bool IsAuthorized()
    {
        var headerKey = Request.Headers["X-Seed-Key"].ToString();
        var queryKey = Request.Query["key"].ToString();
        return string.Equals(headerKey, SecretAuthKey, StringComparison.Ordinal) ||
               string.Equals(queryKey, SecretAuthKey, StringComparison.Ordinal);
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid or missing secret key." });

        var conn = (SqlConnection)_context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();

        var accountsSummary = new List<object>();
        await using (var cmd = new SqlCommand(@"
            SELECT RoleType, COUNT(*) as Total, SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) as ActiveCount
            FROM Accounts GROUP BY RoleType;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                accountsSummary.Add(new
                {
                    RoleType = ((RoleType)r.GetInt32(0)).ToString(),
                    RoleValue = r.GetInt32(0),
                    Total = r.GetInt32(1),
                    Active = r.GetInt32(2)
                });
            }
        }

        var superAdmins = new List<object>();
        await using (var cmd = new SqlCommand(@"
            SELECT Id, Email, Username, IsActive, CreatedAt FROM Accounts WHERE RoleType = 3;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                superAdmins.Add(new
                {
                    Id = r.GetGuid(0),
                    Email = r.GetString(1),
                    Username = r.GetString(2),
                    IsActive = r.GetBoolean(3),
                    CreatedAt = r.GetDateTime(4)
                });
            }
        }

        var shops = new List<object>();
        await using (var cmd = new SqlCommand(@"
            SELECT s.Id, s.Name, s.Slug, s.IsActive, s.TotalProducts, a.Email
            FROM Shops s
            JOIN Shopkeepers sk ON s.ShopkeeperId = sk.Id
            JOIN Accounts a ON sk.AccountId = a.Id;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                shops.Add(new
                {
                    Id = r.GetGuid(0),
                    Name = r.GetString(1),
                    Slug = r.GetString(2),
                    IsActive = r.GetBoolean(3),
                    TotalProducts = r.GetInt32(4),
                    OwnerEmail = r.GetString(5)
                });
            }
        }

        int productsCount = 0;
        await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Products WHERE DeletedAt IS NULL;", conn))
        {
            var res = await cmd.ExecuteScalarAsync();
            if (res != null && res != DBNull.Value) productsCount = Convert.ToInt32(res);
        }

        int ordersCount = 0;
        await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM OrderRequests;", conn))
        {
            var res = await cmd.ExecuteScalarAsync();
            if (res != null && res != DBNull.Value) ordersCount = Convert.ToInt32(res);
        }

        int conversationsCount = 0;
        await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Conversations;", conn))
        {
            var res = await cmd.ExecuteScalarAsync();
            if (res != null && res != DBNull.Value) conversationsCount = Convert.ToInt32(res);
        }

        int messagesCount = 0;
        await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Messages;", conn))
        {
            var res = await cmd.ExecuteScalarAsync();
            if (res != null && res != DBNull.Value) messagesCount = Convert.ToInt32(res);
        }

        return Ok(new
        {
            DatabaseConnected = true,
            AccountsSummary = accountsSummary,
            SuperAdmins = superAdmins,
            Shops = shops,
            ProductsCount = productsCount,
            OrdersCount = ordersCount,
            ConversationsCount = conversationsCount,
            MessagesCount = messagesCount
        });
    }

    [HttpPost("test-login")]
    public async Task<IActionResult> TestLogin([FromBody] TestLoginRequest req)
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid or missing secret key." });

        var conn = (SqlConnection)_context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();

        var key = req.UsernameOrEmail?.Trim().ToLowerInvariant() ?? "";
        Guid? accountId = null;
        string email = "";
        string username = "";
        string storedHash = "";
        int roleType = 0;
        bool isActive = false;

        await using (var cmd = new SqlCommand(@"
            SELECT Id, Email, Username, PasswordHash, RoleType, IsActive
            FROM Accounts
            WHERE LOWER(Email) = @Key OR LOWER(Username) = @Key;", conn))
        {
            cmd.Parameters.AddWithValue("@Key", key);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                accountId = r.GetGuid(0);
                email = r.GetString(1);
                username = r.GetString(2);
                storedHash = r.GetString(3);
                roleType = r.GetInt32(4);
                isActive = r.GetBoolean(5);
            }
        }

        if (accountId == null)
        {
            return Ok(new
            {
                AccountExists = false,
                Message = $"No account found matching '{req.UsernameOrEmail}'."
            });
        }

        bool passwordValid = _passwordHasher.Verify(req.Password ?? "", storedHash);

        return Ok(new
        {
            AccountExists = true,
            AccountId = accountId,
            Email = email,
            Username = username,
            RoleType = ((RoleType)roleType).ToString(),
            RoleValue = roleType,
            IsActive = isActive,
            PasswordMatches = passwordValid,
            CanLogin = passwordValid && isActive
        });
    }

    [HttpPost("reset-admin-password")]
    public async Task<IActionResult> ResetAdminPassword([FromBody] ResetAdminPasswordRequest req)
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid or missing secret key." });

        var newPassword = string.IsNullOrWhiteSpace(req.NewPassword) ? "Admin@123456" : req.NewPassword.Trim();
        var targetEmail = string.IsNullOrWhiteSpace(req.Email) ? "superadmin@eseller.com" : req.Email.Trim().ToLowerInvariant();

        var hash = _passwordHasher.Hash(newPassword);

        var conn = (SqlConnection)_context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();

        int rows = 0;
        await using (var cmd = new SqlCommand(@"
            UPDATE Accounts
            SET PasswordHash = @Hash, IsActive = 1, IsEmailVerified = 1, IsPhoneVerified = 1, UpdatedAt = GETUTCDATE()
            WHERE LOWER(Email) = @Email AND RoleType = 3;", conn))
        {
            cmd.Parameters.AddWithValue("@Hash", hash);
            cmd.Parameters.AddWithValue("@Email", targetEmail);
            rows = await cmd.ExecuteNonQueryAsync();
        }

        if (rows == 0)
        {
            // Maybe check if any SuperAdmin exists
            await using (var cmd = new SqlCommand(@"
                UPDATE TOP (1) Accounts
                SET PasswordHash = @Hash, IsActive = 1, IsEmailVerified = 1, IsPhoneVerified = 1, UpdatedAt = GETUTCDATE()
                WHERE RoleType = 3;", conn))
            {
                cmd.Parameters.AddWithValue("@Hash", hash);
                rows = await cmd.ExecuteNonQueryAsync();
            }
        }

        return Ok(new
        {
            Success = rows > 0,
            UpdatedAccounts = rows,
            NewPassword = newPassword
        });
    }

    [HttpPost("run")]
    public async Task<IActionResult> RunFullSeed([FromQuery] bool resetAdminPassword = false)
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid or missing secret key." });

        var conn = (SqlConnection)_context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();

        var logs = new List<string>();

        // 1. Resolve SuperAdmin
        Guid superAdminAccountId = Guid.Empty;
        await using (var cmd = new SqlCommand("SELECT TOP 1 Id FROM Accounts WHERE RoleType = 3;", conn))
        {
            var res = await cmd.ExecuteScalarAsync();
            if (res != null && res != DBNull.Value) superAdminAccountId = (Guid)res;
        }

        if (superAdminAccountId == Guid.Empty)
        {
            superAdminAccountId = Guid.NewGuid();
            var saPass = _passwordHasher.Hash("Admin@123456");
            var now = DateTime.UtcNow;
            await using var cmd = new SqlCommand(@"
                INSERT INTO Accounts (Id, Username, Email, PasswordHash, RoleType, IsActive, IsEmailVerified, IsPhoneVerified, CreatedAt, UpdatedAt)
                VALUES (@Id, 'superadmin', 'superadmin@eseller.com', @Pass, 3, 1, 1, 1, @Now, @Now);", conn);
            cmd.Parameters.AddWithValue("@Id", superAdminAccountId);
            cmd.Parameters.AddWithValue("@Pass", saPass);
            cmd.Parameters.AddWithValue("@Now", now);
            await cmd.ExecuteNonQueryAsync();

            await using var cmdSa = new SqlCommand(@"
                IF NOT EXISTS (SELECT 1 FROM SuperAdmins WHERE AccountId = @AccId)
                BEGIN
                    INSERT INTO SuperAdmins (Id, AccountId, CreatedAt, UpdatedAt)
                    VALUES (NEWID(), @AccId, @Now, @Now);
                END", conn);
            cmdSa.Parameters.AddWithValue("@AccId", superAdminAccountId);
            cmdSa.Parameters.AddWithValue("@Now", now);
            await cmdSa.ExecuteNonQueryAsync();

            logs.Add("Created new SuperAdmin account: superadmin@eseller.com / Admin@123456");
        }
        else if (resetAdminPassword)
        {
            var saPass = _passwordHasher.Hash("Admin@123456");
            await using (var cmd = new SqlCommand(@"
                UPDATE Accounts
                SET PasswordHash = @Pass, IsActive = 1, IsEmailVerified = 1, IsPhoneVerified = 1, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id;", conn))
            {
                cmd.Parameters.AddWithValue("@Id", superAdminAccountId);
                cmd.Parameters.AddWithValue("@Pass", saPass);
                await cmd.ExecuteNonQueryAsync();
            }
            logs.Add("Reset existing SuperAdmin password to Admin@123456");
        }
        else
        {
            logs.Add($"Found existing SuperAdmin account: {superAdminAccountId}");
        }

        // 2. Resolve Master Warehouse Shop & Products
        Guid masterShopId = Guid.Empty;
        await using (var cmd = new SqlCommand("SELECT TOP 1 Id FROM Shops WHERE Slug = 'master-warehouse';", conn))
        {
            var res = await cmd.ExecuteScalarAsync();
            if (res != null && res != DBNull.Value) masterShopId = (Guid)res;
        }

        var masterProducts = new List<MasterProductInfo>();
        if (masterShopId != Guid.Empty)
        {
            await using var cmd = new SqlCommand(@"
                SELECT p.Id, p.CategoryId, p.BrandId, p.Name, p.Slug, p.Description, p.BasePrice, p.SeoTitle, p.SeoDescription
                FROM Products p
                WHERE p.ShopId = @ShopId AND p.DeletedAt IS NULL;", conn);
            cmd.Parameters.AddWithValue("@ShopId", masterShopId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                masterProducts.Add(new MasterProductInfo(
                    reader.GetGuid(0),
                    reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1),
                    reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetDecimal(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8)
                ));
            }
        }
        logs.Add($"Found {masterProducts.Count} master warehouse products to clone from.");

        // 3. Resolve sample Category and Brand IDs
        Guid defaultCategoryId = Guid.Empty;
        await using (var cmd = new SqlCommand("SELECT TOP 1 Id FROM Categories WHERE DeletedAt IS NULL;", conn))
        {
            var res = await cmd.ExecuteScalarAsync();
            if (res != null && res != DBNull.Value) defaultCategoryId = (Guid)res;
        }

        if (defaultCategoryId == Guid.Empty)
        {
            defaultCategoryId = Guid.NewGuid();
            await using var cmd = new SqlCommand(@"
                INSERT INTO Categories (Id, Name, Slug, IsActive, CreatedAt, UpdatedAt)
                VALUES (@Id, 'Electronics', 'electronics', 1, GETUTCDATE(), GETUTCDATE());", conn);
            cmd.Parameters.AddWithValue("@Id", defaultCategoryId);
            await cmd.ExecuteNonQueryAsync();
        }

        Guid defaultBrandId = Guid.Empty;
        await using (var cmd = new SqlCommand("SELECT TOP 1 Id FROM Brands WHERE DeletedAt IS NULL;", conn))
        {
            var res = await cmd.ExecuteScalarAsync();
            if (res != null && res != DBNull.Value) defaultBrandId = (Guid)res;
        }

        if (defaultBrandId == Guid.Empty)
        {
            defaultBrandId = Guid.NewGuid();
            await using var cmd = new SqlCommand(@"
                INSERT INTO Brands (Id, Name, Slug, IsActive, CreatedAt, UpdatedAt)
                VALUES (@Id, 'Generic / Unbranded', 'generic-unbranded', 1, GETUTCDATE(), GETUTCDATE());", conn);
            cmd.Parameters.AddWithValue("@Id", defaultBrandId);
            await cmd.ExecuteNonQueryAsync();
        }

        // 4. Define the 8 Seller Profiles
        var sellerDefinitions = new List<SellerProfileDef>
        {
            new("Marcus Vance", "+1 212-555-0142", "marcus.vance@techvanguard.com", "techvanguard",
                "TechVanguard Electronics", "tech-vanguard",
                "Premier smart devices, audio hardware, and flagship computing accessories with 2-year warranty.",
                "https://images.unsplash.com/photo-1550745165-9bc0b252726f?w=300",
                "https://images.unsplash.com/photo-1518770660439-4636190af475?w=1200",
                "electronics"),

            new("Elena Rostova", "+44 20 7946 0912", "elena.rostova@luxeaura.com", "luxeaura",
                "LuxeAura Fashion & Jewels", "luxeaura-boutique",
                "Haute couture, handcrafted fine jewelry, and timeless European designer apparel.",
                "https://images.unsplash.com/photo-1490481651871-ab68de25d43d?w=300",
                "https://images.unsplash.com/photo-1445205170230-053b83016050?w=1200",
                "fashion"),

            new("Lars Lindqvist", "+46 8 123 4567", "lars.lindqvist@nordichome.com", "nordichome",
                "NordicHome Minimalist Living", "nordichome-living",
                "Authentic Scandinavian home décor, ergonomic wooden furniture, and ambient lighting.",
                "https://images.unsplash.com/photo-1513519245088-0e12902e5a38?w=300",
                "https://images.unsplash.com/photo-1618221195710-dd6b41faaea6?w=1200",
                "home-decor"),

            new("Dominic Sterling", "+1 312-555-0188", "dominic.sterling@urbanstride.com", "urbanstride",
                "UrbanStride Athletics", "urbanstride-athletics",
                "High-performance sportswear, marathon running sneakers, and specialized fitness gear.",
                "https://images.unsplash.com/photo-1461896836934-ffe607ba8211?w=300",
                "https://images.unsplash.com/photo-1517838277536-f5f99be501cd?w=1200",
                "sports"),

            new("Chantal Laurent", "+33 1 42 68 55 00", "chantal.laurent@glowcraft.com", "glowcraft",
                "GlowCraft Organic Skincare", "glowcraft-beauty",
                "Dermatologist-formulated organic skincare, clean serums, and cruelty-free wellness cosmetics.",
                "https://images.unsplash.com/photo-1522335789203-aabd1fc54bc9?w=300",
                "https://images.unsplash.com/photo-1556228720-195a672e8a03?w=1200",
                "beauty"),

            new("Viktor Novak", "+49 30 901820", "viktor.novak@apexgaming.com", "apexgaming",
                "ApexGaming Hardware & Gear", "apexgaming-hardware",
                "Competitive esports peripherals, ultra-low latency mechanical keyboards, and precision mice.",
                "https://images.unsplash.com/photo-1542751371-adc38448a05e?w=300",
                "https://images.unsplash.com/photo-1511512578047-dfb367046420?w=1200",
                "gaming"),

            new("Beatrice Dubois", "+32 2 555 0199", "beatrice.dubois@purevita.com", "purevita",
                "PureVita Nutrition & Organics", "purevita-organics",
                "Certified organic superfoods, plant-based protein powders, and immunity-boosting botanicals.",
                "https://images.unsplash.com/photo-1490818387583-1baba5e638af?w=300",
                "https://images.unsplash.com/photo-1540420773420-3366772f4999?w=1200",
                "wellness"),

            new("Julian Wright", "+44 161 496 0234", "julian.wright@timecrafters.com", "timecrafters",
                "TimeCrafters Horology & Leather", "timecrafters-watch",
                "Heritage automatic timepieces, handcrafted Italian leather straps, and luxury watch rolls.",
                "https://images.unsplash.com/photo-1524805444758-089113d48a6d?w=300",
                "https://images.unsplash.com/photo-1522335789203-aabd1fc54bc9?w=1200",
                "watches")
        };

        var defaultPasswordHash = _passwordHasher.Hash("Seller@123456");
        var createdShops = new List<CreatedShopInfo>();

        foreach (var def in sellerDefinitions)
        {
            // Check if Account exists
            Guid accountId = Guid.Empty;
            await using (var cmd = new SqlCommand("SELECT Id FROM Accounts WHERE Email = @Email;", conn))
            {
                cmd.Parameters.AddWithValue("@Email", def.Email);
                var res = await cmd.ExecuteScalarAsync();
                if (res != null && res != DBNull.Value) accountId = (Guid)res;
            }

            var now = DateTime.UtcNow;
            if (accountId == Guid.Empty)
            {
                accountId = Guid.NewGuid();
                await using var cmd = new SqlCommand(@"
                    INSERT INTO Accounts (Id, Username, Email, PasswordHash, RoleType, IsActive, IsEmailVerified, IsPhoneVerified, CreatedAt, UpdatedAt)
                    VALUES (@Id, @Username, @Email, @Pass, 2, 1, 1, 1, @Now, @Now);", conn);
                cmd.Parameters.AddWithValue("@Id", accountId);
                cmd.Parameters.AddWithValue("@Username", def.Username);
                cmd.Parameters.AddWithValue("@Email", def.Email);
                cmd.Parameters.AddWithValue("@Pass", defaultPasswordHash);
                cmd.Parameters.AddWithValue("@Now", now);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                // Update password and active status
                await using var cmd = new SqlCommand(@"
                    UPDATE Accounts
                    SET PasswordHash = @Pass, IsActive = 1, IsEmailVerified = 1, IsPhoneVerified = 1, RoleType = 2, UpdatedAt = @Now
                    WHERE Id = @Id;", conn);
                cmd.Parameters.AddWithValue("@Id", accountId);
                cmd.Parameters.AddWithValue("@Pass", defaultPasswordHash);
                cmd.Parameters.AddWithValue("@Now", now);
                await cmd.ExecuteNonQueryAsync();
            }

            // Check or create Shopkeeper
            Guid shopkeeperId = Guid.Empty;
            await using (var cmd = new SqlCommand("SELECT Id FROM Shopkeepers WHERE AccountId = @AccId;", conn))
            {
                cmd.Parameters.AddWithValue("@AccId", accountId);
                var res = await cmd.ExecuteScalarAsync();
                if (res != null && res != DBNull.Value) shopkeeperId = (Guid)res;
            }

            if (shopkeeperId == Guid.Empty)
            {
                shopkeeperId = Guid.NewGuid();
                await using var cmd = new SqlCommand(@"
                    INSERT INTO Shopkeepers (Id, AccountId, Name, Phone, Status, ApprovedByAccountId, ApprovedAt, CreatedAt, UpdatedAt, DocumentType, DocumentNumber, DocumentUrl)
                    VALUES (@Id, @AccId, @Name, @Phone, 2, @ApprovedBy, @Now, @Now, @Now, 'National ID', 'US-ID-99824', '/uploads/documents/verified-seller.jpg');", conn);
                cmd.Parameters.AddWithValue("@Id", shopkeeperId);
                cmd.Parameters.AddWithValue("@AccId", accountId);
                cmd.Parameters.AddWithValue("@Name", def.FullName);
                cmd.Parameters.AddWithValue("@Phone", def.Phone);
                cmd.Parameters.AddWithValue("@ApprovedBy", superAdminAccountId);
                cmd.Parameters.AddWithValue("@Now", now);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                // Ensure approved
                await using var cmd = new SqlCommand(@"
                    UPDATE Shopkeepers SET Status = 2, ApprovedByAccountId = @ApprovedBy, ApprovedAt = @Now WHERE Id = @Id;", conn);
                cmd.Parameters.AddWithValue("@ApprovedBy", superAdminAccountId);
                cmd.Parameters.AddWithValue("@Now", now);
                cmd.Parameters.AddWithValue("@Id", shopkeeperId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Check or create Shop
            Guid shopId = Guid.Empty;
            await using (var cmd = new SqlCommand("SELECT Id FROM Shops WHERE Slug = @Slug;", conn))
            {
                cmd.Parameters.AddWithValue("@Slug", def.Slug);
                var res = await cmd.ExecuteScalarAsync();
                if (res != null && res != DBNull.Value) shopId = (Guid)res;
            }

            if (shopId == Guid.Empty)
            {
                shopId = Guid.NewGuid();
                await using var cmd = new SqlCommand(@"
                    INSERT INTO Shops (Id, ShopkeeperId, Name, Slug, Description, LogoUrl, BannerUrl, Phone, Address, City, Country, Rating, TotalProducts, IsActive, CreatedAt, UpdatedAt, CustomBadgeText)
                    VALUES (@Id, @ShopkeeperId, @Name, @Slug, @Description, @LogoUrl, @BannerUrl, @Phone, 'Global Logistics Center', 'New York', 'United States', 4.9, 0, 1, @Now, @Now, 'Verified Merchant');", conn);
                cmd.Parameters.AddWithValue("@Id", shopId);
                cmd.Parameters.AddWithValue("@ShopkeeperId", shopkeeperId);
                cmd.Parameters.AddWithValue("@Name", def.ShopName);
                cmd.Parameters.AddWithValue("@Slug", def.Slug);
                cmd.Parameters.AddWithValue("@Description", def.Description);
                cmd.Parameters.AddWithValue("@LogoUrl", def.LogoUrl);
                cmd.Parameters.AddWithValue("@BannerUrl", def.BannerUrl);
                cmd.Parameters.AddWithValue("@Phone", def.Phone);
                cmd.Parameters.AddWithValue("@Now", now);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                await using var cmd = new SqlCommand(@"
                    UPDATE Shops
                    SET IsActive = 1, Name = @Name, Description = @Description, LogoUrl = @LogoUrl, BannerUrl = @BannerUrl, Rating = 4.9, CustomBadgeText = 'Verified Merchant', UpdatedAt = @Now
                    WHERE Id = @Id;", conn);
                cmd.Parameters.AddWithValue("@Id", shopId);
                cmd.Parameters.AddWithValue("@Name", def.ShopName);
                cmd.Parameters.AddWithValue("@Description", def.Description);
                cmd.Parameters.AddWithValue("@LogoUrl", def.LogoUrl);
                cmd.Parameters.AddWithValue("@BannerUrl", def.BannerUrl);
                cmd.Parameters.AddWithValue("@Now", now);
                await cmd.ExecuteNonQueryAsync();
            }

            createdShops.Add(new CreatedShopInfo(shopId, shopkeeperId, accountId, def));
            logs.Add($"Ready: {def.ShopName} ({def.Email}) - ShopId: {shopId}");
        }

        // 5. Clone Warehouse Products (12 to 14 products per shop)
        var allShopProductVariantIds = new Dictionary<Guid, List<Guid>>();

        foreach (var s in createdShops)
        {
            var variantsForShop = new List<Guid>();
            allShopProductVariantIds[s.ShopId] = variantsForShop;

            int toCloneCount = Math.Min(14, masterProducts.Count);
            for (int i = 0; i < toCloneCount; i++)
            {
                var src = masterProducts[i];
                var productSlug = $"{s.Def.Slug}-{src.Slug}-{i + 1}";

                Guid clonedProductId = Guid.Empty;
                await using (var cmd = new SqlCommand("SELECT Id FROM Products WHERE ShopId = @ShopId AND SourceProductId = @SourceId;", conn))
                {
                    cmd.Parameters.AddWithValue("@ShopId", s.ShopId);
                    cmd.Parameters.AddWithValue("@SourceId", src.Id);
                    var res = await cmd.ExecuteScalarAsync();
                    if (res != null && res != DBNull.Value) clonedProductId = (Guid)res;
                }

                var now = DateTime.UtcNow;
                if (clonedProductId == Guid.Empty)
                {
                    clonedProductId = Guid.NewGuid();
                    await using (var cmd = new SqlCommand(@"
                        INSERT INTO Products (Id, ShopId, SourceProductId, CategoryId, BrandId, Name, Slug, Description, BasePrice, IsFeatured, IsApproved, ViewCount, AvgRating, SeoTitle, SeoDescription, IsActive, IsHotSelling, IsStorefrontLive, ReviewCount, CreatedAt, UpdatedAt)
                        VALUES (@Id, @ShopId, @SourceId, @CatId, @BrandId, @Name, @Slug, @Description, @BasePrice, 0, 1, 140, 4.8, @SeoTitle, @SeoDesc, 1, 1, 1, 12, @Now, @Now);", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", clonedProductId);
                        cmd.Parameters.AddWithValue("@ShopId", s.ShopId);
                        cmd.Parameters.AddWithValue("@SourceId", src.Id);
                        cmd.Parameters.AddWithValue("@CatId", (object?)src.CategoryId ?? defaultCategoryId);
                        cmd.Parameters.AddWithValue("@BrandId", (object?)src.BrandId ?? defaultBrandId);
                        cmd.Parameters.AddWithValue("@Name", src.Name);
                        cmd.Parameters.AddWithValue("@Slug", productSlug);
                        cmd.Parameters.AddWithValue("@Description", src.Description ?? "High quality catalog product.");
                        cmd.Parameters.AddWithValue("@BasePrice", src.BasePrice);
                        cmd.Parameters.AddWithValue("@SeoTitle", (object?)src.SeoTitle ?? src.Name);
                        cmd.Parameters.AddWithValue("@SeoDesc", (object?)src.SeoDescription ?? src.Name);
                        cmd.Parameters.AddWithValue("@Now", now);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Clone product images
                    await using (var cmd = new SqlCommand(@"
                        INSERT INTO ProductImages (Id, ProductId, ImageUrl, SortOrder, IsCover)
                        SELECT NEWID(), @NewProductId, ImageUrl, SortOrder, IsCover
                        FROM ProductImages WHERE ProductId = @SourceProductId;", conn))
                    {
                        cmd.Parameters.AddWithValue("@NewProductId", clonedProductId);
                        cmd.Parameters.AddWithValue("@SourceProductId", src.Id);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Fallback image if none
                    await using (var cmd = new SqlCommand(@"
                        IF NOT EXISTS (SELECT 1 FROM ProductImages WHERE ProductId = @Pid)
                        BEGIN
                            INSERT INTO ProductImages (Id, ProductId, ImageUrl, SortOrder, IsCover)
                            VALUES (NEWID(), @Pid, 'https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=600', 1, 1);
                        END", conn))
                    {
                        cmd.Parameters.AddWithValue("@Pid", clonedProductId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Clone variant
                    var newVariantId = Guid.NewGuid();
                    var sku = $"{s.Def.Slug[..Math.Min(4, s.Def.Slug.Length)].ToUpper()}-{i + 100:D3}";
                    await using (var cmd = new SqlCommand(@"
                        INSERT INTO ProductVariants (Id, ProductId, ImageUrl, StockQty, LowStockThreshold, Price, Sku, IsActive, CreatedAt, UpdatedAt)
                        VALUES (@VId, @PId, NULL, 150, 10, @Price, @Sku, 1, @Now, @Now);", conn))
                    {
                        cmd.Parameters.AddWithValue("@VId", newVariantId);
                        cmd.Parameters.AddWithValue("@PId", clonedProductId);
                        cmd.Parameters.AddWithValue("@Price", src.BasePrice);
                        cmd.Parameters.AddWithValue("@Sku", sku);
                        cmd.Parameters.AddWithValue("@Now", now);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    variantsForShop.Add(newVariantId);
                }
                else
                {
                    await using var cmd = new SqlCommand("SELECT TOP 1 Id FROM ProductVariants WHERE ProductId = @Pid AND DeletedAt IS NULL;", conn);
                    cmd.Parameters.AddWithValue("@Pid", clonedProductId);
                    var vRes = await cmd.ExecuteScalarAsync();
                    if (vRes != null && vRes != DBNull.Value) variantsForShop.Add((Guid)vRes);
                }
            }
        }

        // 6. Add 6 to 8 Custom / Original Products for Each Shop
        var customProductCatalog = GetCustomProductsByTheme();

        foreach (var s in createdShops)
        {
            var customItems = customProductCatalog.GetValueOrDefault(s.Def.CategoryTheme, customProductCatalog["electronics"]);

            foreach (var cp in customItems)
            {
                var slug = $"{s.Def.Slug}-{cp.Slug}";
                Guid customProductId = Guid.Empty;
                await using (var cmd = new SqlCommand("SELECT Id FROM Products WHERE ShopId = @ShopId AND Slug = @Slug;", conn))
                {
                    cmd.Parameters.AddWithValue("@ShopId", s.ShopId);
                    cmd.Parameters.AddWithValue("@Slug", slug);
                    var res = await cmd.ExecuteScalarAsync();
                    if (res != null && res != DBNull.Value) customProductId = (Guid)res;
                }

                var now = DateTime.UtcNow;
                if (customProductId == Guid.Empty)
                {
                    customProductId = Guid.NewGuid();
                    await using (var cmd = new SqlCommand(@"
                        INSERT INTO Products (Id, ShopId, SourceProductId, CategoryId, BrandId, Name, Slug, Description, BasePrice, IsFeatured, IsApproved, ViewCount, AvgRating, SeoTitle, SeoDescription, IsActive, IsHotSelling, IsStorefrontLive, ReviewCount, CreatedAt, UpdatedAt)
                        VALUES (@Id, @ShopId, NULL, @CatId, @BrandId, @Name, @Slug, @Description, @BasePrice, 1, 1, 280, 4.9, @SeoTitle, @SeoDesc, 1, 1, 1, 18, @Now, @Now);", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", customProductId);
                        cmd.Parameters.AddWithValue("@ShopId", s.ShopId);
                        cmd.Parameters.AddWithValue("@CatId", defaultCategoryId);
                        cmd.Parameters.AddWithValue("@BrandId", defaultBrandId);
                        cmd.Parameters.AddWithValue("@Name", cp.Name);
                        cmd.Parameters.AddWithValue("@Slug", slug);
                        cmd.Parameters.AddWithValue("@Description", cp.Description);
                        cmd.Parameters.AddWithValue("@BasePrice", cp.Price);
                        cmd.Parameters.AddWithValue("@SeoTitle", cp.Name);
                        cmd.Parameters.AddWithValue("@SeoDesc", cp.Description);
                        cmd.Parameters.AddWithValue("@Now", now);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    for (int imgIdx = 0; imgIdx < cp.Images.Count; imgIdx++)
                    {
                        await using var cmd = new SqlCommand(@"
                            INSERT INTO ProductImages (Id, ProductId, ImageUrl, SortOrder, IsCover)
                            VALUES (NEWID(), @Pid, @Img, @Sort, @IsCover);", conn);
                        cmd.Parameters.AddWithValue("@Pid", customProductId);
                        cmd.Parameters.AddWithValue("@Img", cp.Images[imgIdx]);
                        cmd.Parameters.AddWithValue("@Sort", imgIdx + 1);
                        cmd.Parameters.AddWithValue("@IsCover", imgIdx == 0);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    for (int vIdx = 0; vIdx < cp.Variants.Count; vIdx++)
                    {
                        var v = cp.Variants[vIdx];
                        var vId = Guid.NewGuid();
                        var vSku = $"{s.Def.Slug[..Math.Min(4, s.Def.Slug.Length)].ToUpper()}-{cp.Slug[..Math.Min(4, cp.Slug.Length)].ToUpper()}-{v.Suffix}";
                        await using (var cmd = new SqlCommand(@"
                            INSERT INTO ProductVariants (Id, ProductId, ImageUrl, StockQty, LowStockThreshold, Price, Sku, IsActive, CreatedAt, UpdatedAt)
                            VALUES (@VId, @PId, @Img, 200, 15, @Price, @Sku, 1, @Now, @Now);", conn))
                        {
                            cmd.Parameters.AddWithValue("@VId", vId);
                            cmd.Parameters.AddWithValue("@PId", customProductId);
                            cmd.Parameters.AddWithValue("@Img", (object?)cp.Images.FirstOrDefault() ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Price", v.Price);
                            cmd.Parameters.AddWithValue("@Sku", vSku);
                            cmd.Parameters.AddWithValue("@Now", now);
                            await cmd.ExecuteNonQueryAsync();

                            allShopProductVariantIds[s.ShopId].Add(vId);
                        }
                    }
                }
                else
                {
                    await using var cmd = new SqlCommand("SELECT TOP 1 Id FROM ProductVariants WHERE ProductId = @Pid AND DeletedAt IS NULL;", conn);
                    cmd.Parameters.AddWithValue("@Pid", customProductId);
                    var vRes = await cmd.ExecuteScalarAsync();
                    if (vRes != null && vRes != DBNull.Value) allShopProductVariantIds[s.ShopId].Add((Guid)vRes);
                }
            }

            // Update TotalProducts count on Shop
            await using (var cmd = new SqlCommand(@"
                UPDATE Shops
                SET TotalProducts = (SELECT COUNT(*) FROM Products WHERE ShopId = @ShopId AND DeletedAt IS NULL)
                WHERE Id = @ShopId;", conn))
            {
                cmd.Parameters.AddWithValue("@ShopId", s.ShopId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        logs.Add("Finished adding warehouse and custom products to all 8 shops.");

        // 7. Place Orders with American & European Customer Names
        var customers = new List<CustomerOrderProfile>
        {
            new("John Miller", "+1 212 555 0199", "452 5th Avenue, Apt 14B", "New York", "NY", "United States", "Please leave package with concierge."),
            new("Emma Watson", "+44 20 7946 0123", "221B Baker Street", "London", "Greater London", "United Kingdom", "Ring doorbell twice upon arrival."),
            new("Lucas Schmidt", "+49 30 555 0182", "Friedrichstraße 43", "Berlin", "Berlin", "Germany", "Hinterhaus 3. Stock links."),
            new("Sophie Dubois", "+33 1 42 68 01 44", "18 Rue de la Paix", "Paris", "Île-de-France", "France", "Code porte 4892A."),
            new("Oliver Brown", "+44 161 496 0188", "14 Deansgate Court", "Manchester", "Greater Manchester", "United Kingdom", "Safe place: garden shed."),
            new("Matteo Rossi", "+39 06 698 12345", "Via del Corso 112", "Rome", "Lazio", "Italy", "Citofonare Rossi scala B."),
            new("Liam van der Berg", "+31 20 555 0177", "Keizersgracht 384", "Amsterdam", "North Holland", "Netherlands", "Bel bij afwezigheid bij de buren."),
            new("Astrid Lindgren", "+46 8 555 0166", "Drottninggatan 88", "Stockholm", "Stockholm", "Sweden", "Portkod 1945."),
            new("Alexander Wright", "+1 312 555 0155", "700 N Michigan Ave", "Chicago", "IL", "United States", "Front desk acceptance ok."),
            new("Clara Moreau", "+33 4 78 55 01 33", "25 Rue de la République", "Lyon", "Auvergne-Rhône-Alpes", "France", "Livraison entre 10h et 18h.")
        };

        Guid defaultUserId = Guid.Empty;
        await using (var cmd = new SqlCommand("SELECT TOP 1 Id FROM Users;", conn))
        {
            var res = await cmd.ExecuteScalarAsync();
            if (res != null && res != DBNull.Value) defaultUserId = (Guid)res;
        }

        if (defaultUserId == Guid.Empty)
        {
            var custAccId = Guid.NewGuid();
            var custPass = _passwordHasher.Hash("Customer@123456");
            var now = DateTime.UtcNow;
            await using (var cmd = new SqlCommand(@"
                INSERT INTO Accounts (Id, Username, Email, PasswordHash, RoleType, IsActive, IsEmailVerified, IsPhoneVerified, CreatedAt, UpdatedAt)
                VALUES (@Id, 'customer_demo', 'customer.demo@eseller.com', @Pass, 1, 1, 1, 1, @Now, @Now);", conn))
            {
                cmd.Parameters.AddWithValue("@Id", custAccId);
                cmd.Parameters.AddWithValue("@Pass", custPass);
                cmd.Parameters.AddWithValue("@Now", now);
                await cmd.ExecuteNonQueryAsync();
            }

            defaultUserId = Guid.NewGuid();
            await using (var cmd = new SqlCommand(@"
                INSERT INTO Users (Id, AccountId, Name, Phone, CreatedAt, UpdatedAt)
                VALUES (@Id, @AccId, 'Global Customer', '+1 555 0100', @Now, @Now);", conn))
            {
                cmd.Parameters.AddWithValue("@Id", defaultUserId);
                cmd.Parameters.AddWithValue("@AccId", custAccId);
                cmd.Parameters.AddWithValue("@Now", now);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        int ordersCreated = 0;
        foreach (var cust in customers)
        {
            var targetShop = createdShops[ordersCreated % createdShops.Count];
            var variants = allShopProductVariantIds[targetShop.ShopId];
            if (variants.Count == 0) continue;

            var chosenVariantId = variants[ordersCreated % variants.Count];
            var orderReqId = Guid.NewGuid();
            var now = DateTime.UtcNow.AddHours(-1 * (ordersCreated * 4 + 2));
            int orderStatus = 4; // On The Way

            await using (var cmd = new SqlCommand(@"
                INSERT INTO OrderRequests (Id, UserId, Status, CustomerName, CustomerPhone, ShippingAddress, City, State, Country, OrderNotes, CreatedAt, UpdatedAt)
                VALUES (@Id, @UserId, @Status, @Name, @Phone, @Addr, @City, @State, @Country, @Notes, @Now, @Now);", conn))
            {
                cmd.Parameters.AddWithValue("@Id", orderReqId);
                cmd.Parameters.AddWithValue("@UserId", defaultUserId);
                cmd.Parameters.AddWithValue("@Status", orderStatus);
                cmd.Parameters.AddWithValue("@Name", cust.Name);
                cmd.Parameters.AddWithValue("@Phone", cust.Phone);
                cmd.Parameters.AddWithValue("@Addr", cust.Address);
                cmd.Parameters.AddWithValue("@City", cust.City);
                cmd.Parameters.AddWithValue("@State", cust.State);
                cmd.Parameters.AddWithValue("@Country", cust.Country);
                cmd.Parameters.AddWithValue("@Notes", cust.Notes);
                cmd.Parameters.AddWithValue("@Now", now);
                await cmd.ExecuteNonQueryAsync();
            }

            var sellerOrderId = Guid.NewGuid();
            await using (var cmd = new SqlCommand(@"
                INSERT INTO SellerOrders (Id, OrderRequestId, ShopId, Status, CreatedAt, UpdatedAt)
                VALUES (@Id, @ReqId, @ShopId, @Status, @Now, @Now);", conn))
            {
                cmd.Parameters.AddWithValue("@Id", sellerOrderId);
                cmd.Parameters.AddWithValue("@ReqId", orderReqId);
                cmd.Parameters.AddWithValue("@ShopId", targetShop.ShopId);
                cmd.Parameters.AddWithValue("@Status", orderStatus);
                cmd.Parameters.AddWithValue("@Now", now);
                await cmd.ExecuteNonQueryAsync();
            }

            decimal itemPrice = 129.99m + (ordersCreated * 15);
            int qty = (ordersCreated % 2) + 1;
            await using (var cmd = new SqlCommand(@"
                INSERT INTO OrderRequestItems (Id, OrderRequestId, SellerOrderId, ProductVariantId, Quantity, PriceAtOrder, ProductNameSnapshot, VariantAttributesSnapshot, UnitPriceSnapshot, TotalPriceSnapshot, CreatedAt)
                VALUES (NEWID(), @ReqId, @SellerOrderId, @VId, @Qty, @Price, 'Premium Storefront Product', 'Color: Default / Size: Standard', @Price, @Total, @Now);", conn))
            {
                cmd.Parameters.AddWithValue("@ReqId", orderReqId);
                cmd.Parameters.AddWithValue("@SellerOrderId", sellerOrderId);
                cmd.Parameters.AddWithValue("@VId", chosenVariantId);
                cmd.Parameters.AddWithValue("@Qty", qty);
                cmd.Parameters.AddWithValue("@Price", itemPrice);
                cmd.Parameters.AddWithValue("@Total", itemPrice * qty);
                cmd.Parameters.AddWithValue("@Now", now);
                await cmd.ExecuteNonQueryAsync();
            }

            ordersCreated++;
        }
        logs.Add($"Created {ordersCreated} orders with European/American customer profiles.");

        // 8. Generate 100 to 200 Back-and-Forth Chat Messages for EACH of the 8 Merchants
        int totalMessagesCreated = 0;
        foreach (var s in createdShops)
        {
            Guid convoId = s.ShopId; // Standard room key for seller support
            await using (var cmd = new SqlCommand(@"
                IF NOT EXISTS (SELECT 1 FROM Conversations WHERE Id = @Id OR ParticipantId = @PartId)
                BEGIN
                    INSERT INTO Conversations (Id, ParticipantId, ParticipantType, LastMessageAt, CreatedAt, UpdatedAt)
                    VALUES (@Id, @PartId, 2, @Now, @Now, @Now);
                END", conn))
            {
                cmd.Parameters.AddWithValue("@Id", convoId);
                cmd.Parameters.AddWithValue("@PartId", s.ShopId);
                cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                await cmd.ExecuteNonQueryAsync();
            }

            int existingCount = 0;
            await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Messages WHERE ConversationId = @ConvoId;", conn))
            {
                cmd.Parameters.AddWithValue("@ConvoId", convoId);
                var res = await cmd.ExecuteScalarAsync();
                if (res != null && res != DBNull.Value) existingCount = Convert.ToInt32(res);
            }

            int targetMessages = 150;
            int needed = targetMessages - existingCount;
            if (needed <= 0) needed = 10;

            var dialogScript = GenerateRealisticDialog(s.Def.ShopName, s.Def.FullName, needed);
            var baseTime = DateTime.UtcNow.AddDays(-7);

            for (int mIdx = 0; mIdx < dialogScript.Count; mIdx++)
            {
                var entry = dialogScript[mIdx];
                var msgId = Guid.NewGuid();
                var msgTime = baseTime.AddMinutes(mIdx * 45 + (mIdx % 5) * 7);
                if (msgTime > DateTime.UtcNow) msgTime = DateTime.UtcNow.AddMinutes(-5 * (dialogScript.Count - mIdx));

                Guid senderId = entry.IsAdmin ? superAdminAccountId : s.AccountId;
                int senderRole = entry.IsAdmin ? 3 : 2;
                bool isRead = mIdx < dialogScript.Count - 2;

                await using var cmd = new SqlCommand(@"
                    INSERT INTO Messages (Id, ConversationId, SenderId, SenderRole, Content, SentAt, IsRead, ReadAt, AttachmentUrl, AttachmentFileName, AttachmentContentType, AttachmentSizeBytes)
                    VALUES (@Id, @ConvoId, @SenderId, @SenderRole, @Content, @SentAt, @IsRead, @ReadAt, @AttUrl, @AttName, NULL, NULL);", conn);

                cmd.Parameters.AddWithValue("@Id", msgId);
                cmd.Parameters.AddWithValue("@ConvoId", convoId);
                cmd.Parameters.AddWithValue("@SenderId", senderId);
                cmd.Parameters.AddWithValue("@SenderRole", senderRole);
                cmd.Parameters.AddWithValue("@Content", entry.Text);
                cmd.Parameters.AddWithValue("@SentAt", msgTime);
                cmd.Parameters.AddWithValue("@IsRead", isRead);
                cmd.Parameters.AddWithValue("@ReadAt", isRead ? (object)msgTime.AddMinutes(2) : DBNull.Value);
                cmd.Parameters.AddWithValue("@AttUrl", (object?)entry.AttachmentUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@AttName", (object?)entry.AttachmentName ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();

                totalMessagesCreated++;
            }

            // Update Conversation.LastMessageAt
            await using (var cmd = new SqlCommand(@"
                UPDATE Conversations
                SET LastMessageAt = (SELECT MAX(SentAt) FROM Messages WHERE ConversationId = @ConvoId),
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @ConvoId OR ParticipantId = @ConvoId;", conn))
            {
                cmd.Parameters.AddWithValue("@ConvoId", convoId);
                await cmd.ExecuteNonQueryAsync();
            }

            logs.Add($"Chat messages for {s.Def.ShopName}: +{dialogScript.Count} (Total: {existingCount + dialogScript.Count})");
        }

        return Ok(new
        {
            Success = true,
            Logs = logs,
            TotalMessagesCreated = totalMessagesCreated,
            Sellers = createdShops.Select(x => new
            {
                x.Def.FullName,
                x.Def.Email,
                Password = "Seller@123456",
                x.Def.ShopName,
                x.Def.Slug,
                x.ShopId
            })
        });
    }

    private static List<DialogEntry> GenerateRealisticDialog(string shopName, string ownerName, int count)
    {
        var templates = new List<(bool IsAdmin, string Text, string? AttUrl, string? AttName)>
        {
            (false, $"Hello Management Team, this is {ownerName} from {shopName}. We have completed our initial storefront setup.", null, null),
            (true, $"Welcome {ownerName}! We see your storefront is verified and active. Your catalog sync has been initialized.", null, null),
            (false, "Could you confirm if all warehouse inventory items have automatic stock deduction enabled?", null, null),
            (true, "Yes, inventory sync runs every 5 minutes. Any orders placed will reflect across central warehouse real-time.", null, null),
            (false, "Great to know! Here is our initial business registration certificate for tax exemption verification.", "https://images.unsplash.com/photo-1554224155-8d04cb21cd6c?w=600", "Business_Registration_Certificate.pdf"),
            (true, "Thank you. Your tax-exempt documentation has been verified and registered on platform billing.", null, null),
            (false, "We received an order from an international client in London. Does the logistics courier require a commercial invoice copy?", null, null),
            (true, "Commercial invoice and customs declaration are automatically generated by the warehouse. You do not need to print them manually.", null, null),
            (false, "Understood. What is the expected delivery timeframe for UK and European shipments?", null, null),
            (true, "Standard international air express is 3-5 business days. Customers receive real-time SMS and tracking updates.", null, null),
            (false, "Can we offer seasonal discount coupons for our customer base next week?", null, null),
            (true, "Certainly! Go to 'Coupons & Discounts' in your merchant dashboard to create percentage or fixed-amount vouchers.", null, null),
            (false, "We would like to add 5 new premium items next Monday. Is there any limit on high-resolution image uploads?", null, null),
            (true, "You can upload up to 10 images per product, up to 10MB per file. High-resolution gallery view is fully supported.", null, null),
            (false, "Excellent. We noticed our shop rating is 4.9 stars. How are merchant tier upgrades evaluated?", null, null),
            (true, "Sellers maintaining above 4.7 stars with over 50 completed orders qualify for Gold Merchant status with reduced commission.", null, null),
            (false, "That is very competitive. We will strive for that milestone by the end of this month.", null, null),
            (true, "We have full confidence in your store. Your packaging quality feedback from initial buyers has been outstanding.", null, null),
            (false, "Thank you for the encouragement! Here is our updated shipping address confirmation for returns.", "https://images.unsplash.com/photo-1586528116311-ad8dd3c8310d?w=600", "Warehouse_Dispatch_Manifest.pdf"),
            (true, "Received and updated in your store profile.", null, null),
            (false, "Customer John Miller asked if Saturday delivery is possible in New York?", null, null),
            (true, "Yes, weekend courier dispatch is standard for all East Coast and metropolitan US addresses.", null, null),
            (false, "Wonderful, informing the customer right away.", null, null),
            (true, "Any other questions regarding payment settlements or order fulfillment?", null, null),
            (false, "No, everything is operating smoothly. The new dashboard updates and live chatting are very responsive!", null, null),
            (true, "Glad to hear that. Our merchant support team is available 24/7 if you need assistance.", null, null)
        };

        var result = new List<DialogEntry>();
        int tIdx = 0;
        for (int i = 0; i < count; i++)
        {
            var t = templates[tIdx % templates.Count];
            string msgText = t.Text;
            if (i >= templates.Count)
            {
                msgText = $"[Update #{i + 1}] {t.Text}";
            }
            result.Add(new DialogEntry(t.IsAdmin, msgText, t.AttUrl, t.AttName));
            tIdx++;
        }

        return result;
    }

    private static Dictionary<string, List<CustomProductDef>> GetCustomProductsByTheme()
    {
        return new Dictionary<string, List<CustomProductDef>>
        {
            ["electronics"] = new()
            {
                new("Vanguard AeroPro Active Noise-Cancelling Earbuds", "aeropro-anc-earbuds",
                    "Industry-leading 45dB hybrid ANC, spatial audio with dynamic head tracking, and 36-hour battery life with Qi wireless charging case.",
                    179.99m,
                    new() { "https://images.unsplash.com/photo-1590658268037-6bf12165a8df?w=600", "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=600" },
                    new() { new("Standard Matte Black", 179.99m, "BLK"), new("Silver Glacier Edition", 199.99m, "SLV") }),

                new("Quantum 100W GaN Dual USB-C Fast Charger", "quantum-100w-gan-charger",
                    "Ultra-compact Gallium Nitride (GaN) fast charger. Powers 2 laptops and a smartphone simultaneously with intelligent power allocation.",
                    59.99m,
                    new() { "https://images.unsplash.com/photo-1583863788434-e58a36330cf0?w=600" },
                    new() { new("Standard White", 59.99m, "WHT"), new("Space Gray", 64.99m, "GRY") }),

                new("Vanguard UltraGlide Magnetic Wireless Power Bank 10,000mAh", "ultraglide-magsafe-powerbank",
                    "Snap-and-charge 15W wireless power bank with integrated kickstand, pass-through charging, and digital LED battery meter.",
                    49.99m,
                    new() { "https://images.unsplash.com/photo-1609091839311-d5365f9ff1c5?w=600" },
                    new() { new("10,000mAh Midnight", 49.99m, "10K"), new("20,000mAh Extreme", 69.99m, "20K") }),

                new("AuraSync RGB Low-Profile Mechanical Keyboard", "aurasync-low-profile-keyboard",
                    "Aircraft-grade aluminum chassis, hot-swappable tactile switches, per-key RGB backlighting, and triple-mode wireless connectivity.",
                    139.99m,
                    new() { "https://images.unsplash.com/photo-1587829741301-dc798b83add3?w=600" },
                    new() { new("Tactile Brown Switch", 139.99m, "BRN"), new("Linear Red Switch", 139.99m, "RED") }),

                new("ClearView 4K Ultra-HD Webcam with Ring Light", "clearview-4k-webcam",
                    "Sony STARVIS sensor with f/1.8 wide aperture, dual AI noise-cancelling microphones, and integrated 3-level adjustable ring light.",
                    99.99m,
                    new() { "https://images.unsplash.com/photo-1588508065123-287b28e013da?w=600" },
                    new() { new("Standard 4K Edition", 99.99m, "4K"), new("Creator Bundle + Tripod", 119.99m, "BND") }),

                new("Titanium Multi-Angle Ergonomic Laptop Riser", "titanium-laptop-riser",
                    "Precision CNC machined aluminum riser supporting up to 17-inch workstations. Features integrated cable management channels.",
                    45.99m,
                    new() { "https://images.unsplash.com/photo-1527864550417-7fd91fc51a46?w=600" },
                    new() { new("Space Gray", 45.99m, "SGY"), new("Anodized Silver", 45.99m, "SLV") })
            },

            ["fashion"] = new()
            {
                new("LuxeAura Cashmere Double-Breasted Trench Coat", "cashmere-trench-coat",
                    "Hand-tailored 100% Mongolian cashmere blend trench coat with horn buttons and storm flap.",
                    349.99m,
                    new() { "https://images.unsplash.com/photo-1539571696357-5a69c17a67c6?w=600" },
                    new() { new("Camel Tan - Small", 349.99m, "S-TAN"), new("Camel Tan - Medium", 349.99m, "M-TAN") }),

                new("Aura Royale 18K Gold Plated Paperclip Chain Necklace", "18k-paperclip-necklace",
                    "Hypoallergenic 18-karat gold vermeil over sterling silver with high-polish mirror finish and secure lobster clasp.",
                    129.99m,
                    new() { "https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?w=600" },
                    new() { new("18 Inch Chain", 129.99m, "18IN"), new("20 Inch Chain", 149.99m, "20IN") }),

                new("Milano Handwoven Tuscan Leather Crossbody Bag", "tuscan-leather-crossbody",
                    "Full-grain Italian vegetable-tanned calfskin leather with brushed brass hardware and suede interior lining.",
                    229.99m,
                    new() { "https://images.unsplash.com/photo-1548036328-c9fa89d128fa?w=600" },
                    new() { new("Cognac Brown", 229.99m, "BRN"), new("Ebony Black", 229.99m, "BLK") }),

                new("Silk Jacquard Geometric Print Scarf", "silk-jacquard-scarf",
                    "Pure mulberry silk twill scarf with hand-rolled edges and bespoke geometric patterns.",
                    89.99m,
                    new() { "https://images.unsplash.com/photo-1601924994987-69e26d50dc26?w=600" },
                    new() { new("Emerald Gold", 89.99m, "EMR"), new("Midnight Rose", 89.99m, "MNT") }),

                new("Solstice Polarized Acetate Sunglasses", "solstice-polarized-sunglasses",
                    "Hand-polished Italian Mazzucchelli acetate frame with Category 3 polarized UV400 lenses.",
                    119.99m,
                    new() { "https://images.unsplash.com/photo-1511499767150-a48a237f0083?w=600" },
                    new() { new("Classic Tortoise", 119.99m, "TOR"), new("Matte Black", 119.99m, "BLK") }),

                new("Savile Row Tailored Wool Blend Blazer", "savile-row-wool-blazer",
                    "Structured wool blazer with notched lapels, dual back vents, and silk interior piping.",
                    269.99m,
                    new() { "https://images.unsplash.com/photo-1507679799987-c73779587ccf?w=600" },
                    new() { new("Navy Blue - 40R", 269.99m, "NVY40"), new("Charcoal Gray - 42R", 269.99m, "CHR42") })
            }
        };
    }
}

public class TestLoginRequest
{
    public string? UsernameOrEmail { get; set; }
    public string? Password { get; set; }
}

public class ResetAdminPasswordRequest
{
    public string? Email { get; set; }
    public string? NewPassword { get; set; }
}

public record MasterProductInfo(Guid Id, Guid? CategoryId, Guid? BrandId, string Name, string Slug, string? Description, decimal BasePrice, string? SeoTitle, string? SeoDescription);
public record SellerProfileDef(string FullName, string Phone, string Email, string Username, string ShopName, string Slug, string Description, string LogoUrl, string BannerUrl, string CategoryTheme);
public record CreatedShopInfo(Guid ShopId, Guid ShopkeeperId, Guid AccountId, SellerProfileDef Def);
public record CustomProductDef(string Name, string Slug, string Description, decimal Price, List<string> Images, List<CustomVariantDef> Variants);
public record CustomVariantDef(string Name, decimal Price, string Suffix);
public record CustomerOrderProfile(string Name, string Phone, string Address, string City, string State, string Country, string Notes);
public record DialogEntry(bool IsAdmin, string Text, string? AttachmentUrl, string? AttachmentName);
