using Eseller.Application.Abstractions;
using Eseller.Domain.Common;
using Eseller.Domain.Entities.Affiliate;
using Eseller.Domain.Entities.Catalog;
using Eseller.Domain.Entities.Chat;
using Eseller.Domain.Entities.Content;
using Eseller.Domain.Entities.Engagement;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Entities.Orders;
using Eseller.Domain.Entities.Payment;
using Eseller.Domain.Entities.Promotions;
using Eseller.Domain.Entities.Shipping;
using Eseller.Domain.Entities.ShippingConfig;
using Eseller.Domain.Entities.Notifications;
using Eseller.Domain.Entities.Wallet;
using Microsoft.EntityFrameworkCore;


namespace Eseller.Infrastructure.Persistence;

public class EsellerDbContext : DbContext,IApplicationDbContext
{
    public EsellerDbContext(DbContextOptions<EsellerDbContext> options)
        : base(options)
    {
    }

    // Identity & Access
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Shopkeeper> Shopkeepers => Set<Shopkeeper>();
    public DbSet<SuperAdmin> SuperAdmins => Set<SuperAdmin>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<Affiliate> Affiliates => Set<Affiliate>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<VerificationToken> VerificationTokens => Set<VerificationToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Address> Addresses => Set<Address>();

    public DbSet<LocationLog> LocationLogs => Set<LocationLog>();

    // Catalog
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<ShopCategory> ShopCategories => Set<ShopCategory>();
    public DbSet<TierUpgradeRequest> TierUpgradeRequests => Set<TierUpgradeRequest>();
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<ShopImage> ShopImages => Set<ShopImage>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<VariantAttributeValue> VariantAttributeValues => Set<VariantAttributeValue>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    // Cart & Orders
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<OrderRequest> OrderRequests => Set<OrderRequest>();
    public DbSet<SellerOrder> SellerOrders => Set<SellerOrder>();
    public DbSet<OrderRequestItem> OrderRequestItems => Set<OrderRequestItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    // Returns & Shipping
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();
    public DbSet<Refund> Refunds => Set<Refund>();

    // Customer Engagement
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<CompareItem> CompareItems => Set<CompareItem>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ProductQuestion> ProductQuestions => Set<ProductQuestion>();

    // Promotions
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponProduct> CouponProducts => Set<CouponProduct>();
    public DbSet<CouponCategory> CouponCategories => Set<CouponCategory>();
    public DbSet<CouponShop> CouponShops => Set<CouponShop>();
    public DbSet<CouponCustomer> CouponCustomers => Set<CouponCustomer>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();
    public DbSet<FlashSale> FlashSales => Set<FlashSale>();
    public DbSet<FlashSaleProduct> FlashSaleProducts => Set<FlashSaleProduct>();

    // Affiliate
    public DbSet<AffiliateClick> AffiliateClicks => Set<AffiliateClick>();
    public DbSet<AffiliateCommission> AffiliateCommissions => Set<AffiliateCommission>();

    // Wallet & Notifications
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ProductStockAlert> ProductStockAlerts => Set<ProductStockAlert>();

    // Content & Settings
    public DbSet<BlogCategory> BlogCategories => Set<BlogCategory>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<CmsPage> CmsPages => Set<CmsPage>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<HomepageBanner> HomepageBanners => Set<HomepageBanner>();

    // Shipping Config
    public DbSet<ShippingZone> ShippingZones => Set<ShippingZone>();
    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();

    // Payment & Commission (Reserved)
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<SellerEarning> SellerEarnings => Set<SellerEarning>();
    public DbSet<Withdrawal> Withdrawals => Set<Withdrawal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ShopCategory>(e =>
        {
            e.ToTable("ShopCategories");
            e.Property(x => x.PriceUsd).HasPrecision(18, 2);
        });
        modelBuilder.Entity<TierUpgradeRequest>(e =>
        {
            e.ToTable("TierUpgradeRequests");
            e.Property(x => x.Price).HasPrecision(18, 2);
        });
        // Apply all IEntityTypeConfiguration<T> from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EsellerDbContext).Assembly);
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SetCreatedAt(now);
                    entry.Entity.SetUpdatedAt(now);
                    break;

                case EntityState.Modified:
                    entry.Entity.SetUpdatedAt(now);
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}