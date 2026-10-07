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

namespace Eseller.Application.Abstractions;

public interface IApplicationDbContext
{
    // Identity & Access
    DbSet<Account> Accounts { get; }
    DbSet<User> Users { get; }
    DbSet<Shopkeeper> Shopkeepers { get; }
    DbSet<SuperAdmin> SuperAdmins { get; }
    DbSet<Partner> Partners { get; }
    DbSet<Affiliate> Affiliates { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<VerificationToken> VerificationTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<LocationLog> LocationLogs { get; }
    DbSet<Address> Addresses { get; }

    // Catalog
    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Shop> Shops { get; }
    DbSet<ShopCategory> ShopCategories { get; }
    DbSet<TierUpgradeRequest> TierUpgradeRequests { get; }
    DbSet<ShopImage> ShopImages { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<VariantAttributeValue> VariantAttributeValues { get; }
    DbSet<Discount> Discounts { get; }
    DbSet<InventoryTransaction> InventoryTransactions { get; }

    // Cart & Orders
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<OrderRequest> OrderRequests { get; }
    DbSet<SellerOrder> SellerOrders { get; }
    DbSet<OrderRequestItem> OrderRequestItems { get; }
    DbSet<OrderStatusHistory> OrderStatusHistories { get; }
    DbSet<ChatMessage> ChatMessages { get; }

    // Support chat (Customer|Seller ↔ Super Admin)
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }

    // Returns & Shipping
    DbSet<Shipment> Shipments { get; }
    DbSet<ReturnRequest> ReturnRequests { get; }
    DbSet<ReturnItem> ReturnItems { get; }
    DbSet<Refund> Refunds { get; }
    // Customer Engagement
    DbSet<Wishlist> Wishlists { get; }
    DbSet<CompareItem> CompareItems { get; }
    DbSet<Review> Reviews { get; }
    DbSet<ProductQuestion> ProductQuestions { get; }

    // Promotions
    DbSet<Coupon> Coupons { get; }
    DbSet<CouponProduct> CouponProducts { get; }
    DbSet<CouponCategory> CouponCategories { get; }
    DbSet<CouponShop> CouponShops { get; }
    DbSet<CouponCustomer> CouponCustomers { get; }
    DbSet<CouponUsage> CouponUsages { get; }
    DbSet<FlashSale> FlashSales { get; }
    DbSet<FlashSaleProduct> FlashSaleProducts { get; }

    // Affiliate
    DbSet<AffiliateClick> AffiliateClicks { get; }
    DbSet<AffiliateCommission> AffiliateCommissions { get; }

    // Wallet & Notifications
    DbSet<Wallet> Wallets { get; }
    DbSet<WalletTransaction> WalletTransactions { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<ProductStockAlert> ProductStockAlerts { get; }

    // Content & Settings
    DbSet<BlogCategory> BlogCategories { get; }
    DbSet<BlogPost> BlogPosts { get; }
    DbSet<CmsPage> CmsPages { get; }
    DbSet<Setting> Settings { get; }
    DbSet<HomepageBanner> HomepageBanners { get; }
    DbSet<PaymentShowcaseLogo> PaymentShowcaseLogos { get; }

    // Shipping Config
    DbSet<ShippingZone> ShippingZones { get; }
    DbSet<ShippingMethod> ShippingMethods { get; }

    // Payment & Commission (Reserved)
    DbSet<CommissionRule> CommissionRules { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PaymentAttempt> PaymentAttempts { get; }
    DbSet<SellerEarning> SellerEarnings { get; }
    DbSet<Withdrawal> Withdrawals { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}