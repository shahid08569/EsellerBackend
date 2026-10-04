IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Accounts] (
        [Id] uniqueidentifier NOT NULL,
        [Username] nvarchar(50) NOT NULL,
        [Email] nvarchar(150) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [RoleType] int NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsEmailVerified] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsPhoneVerified] bit NOT NULL DEFAULT CAST(0 AS bit),
        [LastLoginAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [BlogCategories] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Slug] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_BlogCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Brands] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Slug] nvarchar(200) NOT NULL,
        [LogoUrl] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [DeletedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Brands] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] uniqueidentifier NOT NULL,
        [ParentCategoryId] uniqueidentifier NULL,
        [Name] nvarchar(150) NOT NULL,
        [Slug] nvarchar(200) NOT NULL,
        [ImageUrl] nvarchar(500) NULL,
        [SeoTitle] nvarchar(200) NULL,
        [SeoDescription] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [DeletedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Categories_Categories_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [CmsPages] (
        [Id] uniqueidentifier NOT NULL,
        [Slug] nvarchar(200) NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CmsPages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [CommissionRules] (
        [Id] uniqueidentifier NOT NULL,
        [ScopeType] int NOT NULL,
        [ScopeId] uniqueidentifier NULL,
        [Type] int NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CommissionRules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [FlashSales] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_FlashSales] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [HomepageBanners] (
        [Id] uniqueidentifier NOT NULL,
        [ImageUrl] nvarchar(500) NOT NULL,
        [LinkUrl] nvarchar(500) NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_HomepageBanners] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Settings] (
        [Id] uniqueidentifier NOT NULL,
        [Key] nvarchar(100) NOT NULL,
        [Value] nvarchar(max) NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Settings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [ShippingZones] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Regions] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ShippingZones] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Affiliates] (
        [Id] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [ReferralCode] nvarchar(50) NOT NULL,
        [CommissionRate] decimal(5,2) NULL,
        [TotalClicks] int NOT NULL DEFAULT 0,
        [TotalConversions] int NOT NULL DEFAULT 0,
        [TotalEarned] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Affiliates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Affiliates_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [ActorAccountId] uniqueidentifier NULL,
        [ActorType] nvarchar(50) NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [IpAddress] nvarchar(45) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_Accounts_ActorAccountId] FOREIGN KEY ([ActorAccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Coupons] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Type] int NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [MinOrderAmount] decimal(18,2) NULL,
        [MaxDiscount] decimal(18,2) NULL,
        [ExpiryDate] datetime2 NULL,
        [UsageLimit] int NULL,
        [UsedCount] int NOT NULL DEFAULT 0,
        [ScopeType] int NOT NULL,
        [CreatedByAccountId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Coupons] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Coupons_Accounts_CreatedByAccountId] FOREIGN KEY ([CreatedByAccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [LocationLogs] (
        [Id] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NULL,
        [Latitude] decimal(10,7) NOT NULL,
        [Longitude] decimal(10,7) NOT NULL,
        [City] nvarchar(100) NULL,
        [Country] nvarchar(100) NULL,
        [CountryCode] nvarchar(5) NULL,
        [DeviceType] nvarchar(20) NOT NULL,
        [IpAddress] nvarchar(45) NULL,
        [UserAgent] nvarchar(500) NULL,
        [Action] nvarchar(50) NOT NULL,
        [RecordedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_LocationLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LocationLogs_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Message] nvarchar(2000) NOT NULL,
        [IsRead] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Partners] (
        [Id] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [AddedBySuperAdminId] uniqueidentifier NOT NULL,
        [AddedAt] datetime2 NOT NULL,
        [Permissions] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Partners] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Partners_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Partners_Accounts_AddedBySuperAdminId] FOREIGN KEY ([AddedBySuperAdminId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(500) NOT NULL,
        [FamilyId] uniqueidentifier NOT NULL,
        [ReplacedByTokenId] uniqueidentifier NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RefreshTokens_RefreshTokens_ReplacedByTokenId] FOREIGN KEY ([ReplacedByTokenId]) REFERENCES [RefreshTokens] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Shopkeepers] (
        [Id] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Phone] nvarchar(30) NOT NULL,
        [Status] int NOT NULL,
        [ApprovedByAccountId] uniqueidentifier NULL,
        [ApprovedAt] datetime2 NULL,
        [RejectionReason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Shopkeepers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Shopkeepers_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Shopkeepers_Accounts_ApprovedByAccountId] FOREIGN KEY ([ApprovedByAccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [SuperAdmins] (
        [Id] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SuperAdmins] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SuperAdmins_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Phone] nvarchar(30) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [VerificationTokens] (
        [Id] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [Purpose] int NOT NULL,
        [CodeHash] nvarchar(500) NOT NULL,
        [Channel] int NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [Attempts] int NOT NULL DEFAULT 0,
        [ConsumedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_VerificationTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VerificationTokens_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Wallets] (
        [Id] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [Balance] decimal(18,2) NOT NULL DEFAULT 0.0,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Wallets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Wallets_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Withdrawals] (
        [Id] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PayoutMethod] nvarchar(100) NULL,
        [Status] int NOT NULL,
        [RequestedAt] datetime2 NOT NULL,
        [ProcessedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Withdrawals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Withdrawals_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [BlogPosts] (
        [Id] uniqueidentifier NOT NULL,
        [BlogCategoryId] uniqueidentifier NULL,
        [Title] nvarchar(300) NOT NULL,
        [Slug] nvarchar(350) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [CoverImageUrl] nvarchar(500) NULL,
        [IsPublished] bit NOT NULL DEFAULT CAST(0 AS bit),
        [PublishedAt] datetime2 NULL,
        [SeoTitle] nvarchar(200) NULL,
        [SeoDescription] nvarchar(500) NULL,
        [CreatedByAccountId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BlogPosts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BlogPosts_Accounts_CreatedByAccountId] FOREIGN KEY ([CreatedByAccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BlogPosts_BlogCategories_BlogCategoryId] FOREIGN KEY ([BlogCategoryId]) REFERENCES [BlogCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [CouponCategories] (
        [Id] uniqueidentifier NOT NULL,
        [CouponId] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_CouponCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CouponCategories_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CouponCategories_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Shops] (
        [Id] uniqueidentifier NOT NULL,
        [ShopkeeperId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Slug] nvarchar(250) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [LogoUrl] nvarchar(500) NULL,
        [BannerUrl] nvarchar(500) NULL,
        [Phone] nvarchar(30) NULL,
        [Address] nvarchar(500) NULL,
        [City] nvarchar(100) NULL,
        [Country] nvarchar(100) NULL,
        [Rating] decimal(3,2) NOT NULL,
        [TotalProducts] int NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [DeletedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Shops] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Shops_Shopkeepers_ShopkeeperId] FOREIGN KEY ([ShopkeeperId]) REFERENCES [Shopkeepers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Addresses] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Label] nvarchar(50) NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [Phone] nvarchar(30) NOT NULL,
        [AddressLine1] nvarchar(500) NOT NULL,
        [AddressLine2] nvarchar(500) NULL,
        [City] nvarchar(100) NOT NULL,
        [State] nvarchar(100) NULL,
        [Country] nvarchar(100) NOT NULL,
        [PostalCode] nvarchar(20) NULL,
        [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [DeletedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Addresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Addresses_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Carts] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Carts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Carts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [CouponCustomers] (
        [Id] uniqueidentifier NOT NULL,
        [CouponId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_CouponCustomers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CouponCustomers_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CouponCustomers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderRequests] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [AffiliateId] uniqueidentifier NULL,
        [ReferralCodeSnapshot] nvarchar(50) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderRequests_Affiliates_AffiliateId] FOREIGN KEY ([AffiliateId]) REFERENCES [Affiliates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderRequests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [WalletTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [WalletId] uniqueidentifier NOT NULL,
        [Direction] int NOT NULL,
        [TransactionType] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ReferenceType] nvarchar(100) NULL,
        [ReferenceId] uniqueidentifier NULL,
        [Description] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_WalletTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WalletTransactions_Wallets_WalletId] FOREIGN KEY ([WalletId]) REFERENCES [Wallets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [CouponShops] (
        [Id] uniqueidentifier NOT NULL,
        [CouponId] uniqueidentifier NOT NULL,
        [ShopId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_CouponShops] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CouponShops_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CouponShops_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] uniqueidentifier NOT NULL,
        [ShopId] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [BrandId] uniqueidentifier NULL,
        [Name] nvarchar(250) NOT NULL,
        [Slug] nvarchar(300) NOT NULL,
        [Description] nvarchar(4000) NULL,
        [BasePrice] decimal(18,2) NOT NULL,
        [IsFeatured] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsApproved] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ViewCount] int NOT NULL DEFAULT 0,
        [AvgRating] decimal(3,2) NULL,
        [SeoTitle] nvarchar(200) NULL,
        [SeoDescription] nvarchar(500) NULL,
        [RejectionReason] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [DeletedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Brands_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [Brands] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Products_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [ShippingMethods] (
        [Id] uniqueidentifier NOT NULL,
        [ShopId] uniqueidentifier NULL,
        [ZoneId] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Rate] decimal(18,2) NOT NULL,
        [IsFree] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ShippingMethods] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShippingMethods_ShippingZones_ZoneId] FOREIGN KEY ([ZoneId]) REFERENCES [ShippingZones] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ShippingMethods_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [ShopImages] (
        [Id] uniqueidentifier NOT NULL,
        [ShopId] uniqueidentifier NOT NULL,
        [ImageUrl] nvarchar(500) NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_ShopImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ShopImages_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [ChatMessages] (
        [Id] uniqueidentifier NOT NULL,
        [OrderRequestId] uniqueidentifier NOT NULL,
        [SenderAccountId] uniqueidentifier NOT NULL,
        [SenderRole] int NOT NULL,
        [Message] nvarchar(4000) NOT NULL,
        [SentAt] datetime2 NOT NULL,
        [IsRead] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ReadAt] datetime2 NULL,
        [DeliveredAt] datetime2 NULL,
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatMessages_Accounts_SenderAccountId] FOREIGN KEY ([SenderAccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChatMessages_OrderRequests_OrderRequestId] FOREIGN KEY ([OrderRequestId]) REFERENCES [OrderRequests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [CouponUsages] (
        [Id] uniqueidentifier NOT NULL,
        [CouponId] uniqueidentifier NOT NULL,
        [OrderRequestId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [UsedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CouponUsages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CouponUsages_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CouponUsages_OrderRequests_OrderRequestId] FOREIGN KEY ([OrderRequestId]) REFERENCES [OrderRequests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CouponUsages_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderStatusHistories] (
        [Id] uniqueidentifier NOT NULL,
        [OrderRequestId] uniqueidentifier NOT NULL,
        [OldStatus] int NOT NULL,
        [NewStatus] int NOT NULL,
        [ChangedByAccountId] uniqueidentifier NULL,
        [ChangedByType] nvarchar(50) NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderStatusHistories_Accounts_ChangedByAccountId] FOREIGN KEY ([ChangedByAccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderStatusHistories_OrderRequests_OrderRequestId] FOREIGN KEY ([OrderRequestId]) REFERENCES [OrderRequests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] uniqueidentifier NOT NULL,
        [OrderRequestId] uniqueidentifier NOT NULL,
        [Method] int NOT NULL,
        [Provider] nvarchar(100) NULL,
        [ProviderTransactionId] nvarchar(200) NULL,
        [Currency] nvarchar(10) NOT NULL DEFAULT N'PKR',
        [Amount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_OrderRequests_OrderRequestId] FOREIGN KEY ([OrderRequestId]) REFERENCES [OrderRequests] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [SellerOrders] (
        [Id] uniqueidentifier NOT NULL,
        [OrderRequestId] uniqueidentifier NOT NULL,
        [ShopId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SellerOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SellerOrders_OrderRequests_OrderRequestId] FOREIGN KEY ([OrderRequestId]) REFERENCES [OrderRequests] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SellerOrders_Shops_ShopId] FOREIGN KEY ([ShopId]) REFERENCES [Shops] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [AffiliateClicks] (
        [Id] uniqueidentifier NOT NULL,
        [AffiliateId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NULL,
        [ClickedAt] datetime2 NOT NULL,
        [IpAddress] nvarchar(45) NULL,
        [UserAgent] nvarchar(500) NULL,
        [Converted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_AffiliateClicks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AffiliateClicks_Affiliates_AffiliateId] FOREIGN KEY ([AffiliateId]) REFERENCES [Affiliates] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AffiliateClicks_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [CompareItems] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CompareItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CompareItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CompareItems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [CouponProducts] (
        [Id] uniqueidentifier NOT NULL,
        [CouponId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_CouponProducts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CouponProducts_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CouponProducts_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Discounts] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NULL,
        [ScopeType] int NOT NULL,
        [ScopeId] uniqueidentifier NULL,
        [Type] int NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Discounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Discounts_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [FlashSaleProducts] (
        [Id] uniqueidentifier NOT NULL,
        [FlashSaleId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [DiscountType] int NOT NULL,
        [DiscountValue] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_FlashSaleProducts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FlashSaleProducts_FlashSales_FlashSaleId] FOREIGN KEY ([FlashSaleId]) REFERENCES [FlashSales] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_FlashSaleProducts_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductImages] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ImageUrl] nvarchar(500) NOT NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductQuestions] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Question] nvarchar(2000) NOT NULL,
        [IsApproved] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Answer] nvarchar(4000) NULL,
        [AnsweredByShopkeeperId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [AnsweredAt] datetime2 NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedByAccountId] uniqueidentifier NULL,
        CONSTRAINT [PK_ProductQuestions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductQuestions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductQuestions_Shopkeepers_AnsweredByShopkeeperId] FOREIGN KEY ([AnsweredByShopkeeperId]) REFERENCES [Shopkeepers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductQuestions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductVariants] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ImageUrl] nvarchar(500) NULL,
        [StockQty] int NOT NULL DEFAULT 0,
        [LowStockThreshold] int NOT NULL DEFAULT 5,
        [Price] decimal(18,2) NOT NULL,
        [Sku] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [DeletedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProductVariants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductVariants_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Wishlists] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Wishlists] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Wishlists_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Wishlists_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [PaymentAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentId] uniqueidentifier NOT NULL,
        [AttemptNumber] int NOT NULL,
        [Status] int NOT NULL,
        [ProviderResponse] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentAttempts_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [ReturnRequests] (
        [Id] uniqueidentifier NOT NULL,
        [SellerOrderId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Status] int NOT NULL,
        [ProcessedByAccountId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ReturnRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReturnRequests_Accounts_ProcessedByAccountId] FOREIGN KEY ([ProcessedByAccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReturnRequests_SellerOrders_SellerOrderId] FOREIGN KEY ([SellerOrderId]) REFERENCES [SellerOrders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReturnRequests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [SellerEarnings] (
        [Id] uniqueidentifier NOT NULL,
        [SellerOrderId] uniqueidentifier NOT NULL,
        [GrossAmount] decimal(18,2) NOT NULL,
        [CommissionAmount] decimal(18,2) NOT NULL,
        [CommissionRuleId] uniqueidentifier NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_SellerEarnings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SellerEarnings_CommissionRules_CommissionRuleId] FOREIGN KEY ([CommissionRuleId]) REFERENCES [CommissionRules] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SellerEarnings_SellerOrders_SellerOrderId] FOREIGN KEY ([SellerOrderId]) REFERENCES [SellerOrders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Shipments] (
        [Id] uniqueidentifier NOT NULL,
        [SellerOrderId] uniqueidentifier NOT NULL,
        [ShippingMethodId] uniqueidentifier NULL,
        [Carrier] nvarchar(100) NULL,
        [TrackingNumber] nvarchar(100) NULL,
        [Status] int NOT NULL,
        [ShippedAt] datetime2 NULL,
        [DeliveredAt] datetime2 NULL,
        [EstimatedDeliveryAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Shipments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Shipments_SellerOrders_SellerOrderId] FOREIGN KEY ([SellerOrderId]) REFERENCES [SellerOrders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Shipments_ShippingMethods_ShippingMethodId] FOREIGN KEY ([ShippingMethodId]) REFERENCES [ShippingMethods] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [CartItems] (
        [Id] uniqueidentifier NOT NULL,
        [CartId] uniqueidentifier NOT NULL,
        [ProductVariantId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CartItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CartItems_Carts_CartId] FOREIGN KEY ([CartId]) REFERENCES [Carts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CartItems_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [InventoryTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [ProductVariantId] uniqueidentifier NOT NULL,
        [TransactionType] int NOT NULL,
        [Quantity] int NOT NULL,
        [PreviousQuantity] int NOT NULL,
        [NewQuantity] int NOT NULL,
        [ReferenceType] nvarchar(100) NOT NULL,
        [ReferenceId] uniqueidentifier NULL,
        [CreatedByAccountId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_InventoryTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryTransactions_Accounts_CreatedByAccountId] FOREIGN KEY ([CreatedByAccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryTransactions_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderRequestItems] (
        [Id] uniqueidentifier NOT NULL,
        [OrderRequestId] uniqueidentifier NOT NULL,
        [SellerOrderId] uniqueidentifier NOT NULL,
        [ProductVariantId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [PriceAtOrder] decimal(18,2) NOT NULL,
        [ProductNameSnapshot] nvarchar(250) NOT NULL,
        [VariantAttributesSnapshot] nvarchar(max) NULL,
        [UnitPriceSnapshot] decimal(18,2) NOT NULL,
        [TotalPriceSnapshot] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderRequestItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderRequestItems_OrderRequests_OrderRequestId] FOREIGN KEY ([OrderRequestId]) REFERENCES [OrderRequests] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderRequestItems_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderRequestItems_SellerOrders_SellerOrderId] FOREIGN KEY ([SellerOrderId]) REFERENCES [SellerOrders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [VariantAttributeValues] (
        [Id] uniqueidentifier NOT NULL,
        [ProductVariantId] uniqueidentifier NOT NULL,
        [AttributeName] nvarchar(100) NOT NULL,
        [AttributeValue] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_VariantAttributeValues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VariantAttributeValues_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Refunds] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentId] uniqueidentifier NULL,
        [ReturnRequestId] uniqueidentifier NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [RefundMethod] nvarchar(100) NULL,
        [ProviderReference] nvarchar(200) NULL,
        [ProcessedByAccountId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Refunds] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Refunds_Accounts_ProcessedByAccountId] FOREIGN KEY ([ProcessedByAccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Refunds_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Refunds_ReturnRequests_ReturnRequestId] FOREIGN KEY ([ReturnRequestId]) REFERENCES [ReturnRequests] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [AffiliateCommissions] (
        [Id] uniqueidentifier NOT NULL,
        [AffiliateId] uniqueidentifier NOT NULL,
        [OrderRequestId] uniqueidentifier NOT NULL,
        [OrderRequestItemId] uniqueidentifier NOT NULL,
        [AffiliateClickId] uniqueidentifier NULL,
        [CommissionAmount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ApprovedAt] datetime2 NULL,
        [PaidAt] datetime2 NULL,
        CONSTRAINT [PK_AffiliateCommissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AffiliateCommissions_AffiliateClicks_AffiliateClickId] FOREIGN KEY ([AffiliateClickId]) REFERENCES [AffiliateClicks] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AffiliateCommissions_Affiliates_AffiliateId] FOREIGN KEY ([AffiliateId]) REFERENCES [Affiliates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AffiliateCommissions_OrderRequestItems_OrderRequestItemId] FOREIGN KEY ([OrderRequestItemId]) REFERENCES [OrderRequestItems] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AffiliateCommissions_OrderRequests_OrderRequestId] FOREIGN KEY ([OrderRequestId]) REFERENCES [OrderRequests] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [ReturnItems] (
        [Id] uniqueidentifier NOT NULL,
        [ReturnRequestId] uniqueidentifier NOT NULL,
        [OrderRequestItemId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        CONSTRAINT [PK_ReturnItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReturnItems_OrderRequestItems_OrderRequestItemId] FOREIGN KEY ([OrderRequestItemId]) REFERENCES [OrderRequestItems] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReturnItems_ReturnRequests_ReturnRequestId] FOREIGN KEY ([ReturnRequestId]) REFERENCES [ReturnRequests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE TABLE [Reviews] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [OrderRequestItemId] uniqueidentifier NULL,
        [Rating] int NOT NULL,
        [Title] nvarchar(200) NULL,
        [Comment] nvarchar(4000) NULL,
        [IsApproved] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reviews_OrderRequestItems_OrderRequestItemId] FOREIGN KEY ([OrderRequestItemId]) REFERENCES [OrderRequestItems] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reviews_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Reviews_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Accounts_Email] ON [Accounts] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Accounts_Username] ON [Accounts] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Addresses_UserId] ON [Addresses] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Addresses_UserId_IsDefault] ON [Addresses] ([UserId], [IsDefault]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AffiliateClicks_AffiliateId] ON [AffiliateClicks] ([AffiliateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AffiliateClicks_AffiliateId_Converted] ON [AffiliateClicks] ([AffiliateId], [Converted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AffiliateClicks_ClickedAt] ON [AffiliateClicks] ([ClickedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AffiliateClicks_ProductId] ON [AffiliateClicks] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AffiliateCommissions_AffiliateClickId] ON [AffiliateCommissions] ([AffiliateClickId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AffiliateCommissions_AffiliateId] ON [AffiliateCommissions] ([AffiliateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AffiliateCommissions_AffiliateId_Status] ON [AffiliateCommissions] ([AffiliateId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AffiliateCommissions_OrderRequestId] ON [AffiliateCommissions] ([OrderRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AffiliateCommissions_OrderRequestItemId] ON [AffiliateCommissions] ([OrderRequestItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AffiliateCommissions_Status] ON [AffiliateCommissions] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Affiliates_AccountId] ON [Affiliates] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Affiliates_ReferralCode] ON [Affiliates] ([ReferralCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ActorAccountId] ON [AuditLogs] ([ActorAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedAt] ON [AuditLogs] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [AuditLogs] ([EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BlogCategories_Slug] ON [BlogCategories] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BlogPosts_BlogCategoryId] ON [BlogPosts] ([BlogCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BlogPosts_CreatedByAccountId] ON [BlogPosts] ([CreatedByAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BlogPosts_IsPublished] ON [BlogPosts] ([IsPublished]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BlogPosts_Slug] ON [BlogPosts] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Brands_IsActive] ON [Brands] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Brands_Slug] ON [Brands] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CartItems_CartId_ProductVariantId] ON [CartItems] ([CartId], [ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CartItems_ProductVariantId] ON [CartItems] ([ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Carts_UserId] ON [Carts] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Categories_IsActive] ON [Categories] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Categories_ParentCategoryId] ON [Categories] ([ParentCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_OrderRequestId] ON [ChatMessages] ([OrderRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_OrderRequestId_IsRead] ON [ChatMessages] ([OrderRequestId], [IsRead]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_SenderAccountId] ON [ChatMessages] ([SenderAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_SentAt] ON [ChatMessages] ([SentAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CmsPages_Slug] ON [CmsPages] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CommissionRules_IsActive] ON [CommissionRules] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CommissionRules_ScopeType_ScopeId] ON [CommissionRules] ([ScopeType], [ScopeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CompareItems_ProductId] ON [CompareItems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CompareItems_UserId_ProductId] ON [CompareItems] ([UserId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CouponCategories_CategoryId] ON [CouponCategories] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CouponCategories_CouponId_CategoryId] ON [CouponCategories] ([CouponId], [CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CouponCustomers_CouponId_UserId] ON [CouponCustomers] ([CouponId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CouponCustomers_UserId] ON [CouponCustomers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CouponProducts_CouponId_ProductId] ON [CouponProducts] ([CouponId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CouponProducts_ProductId] ON [CouponProducts] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Coupons_Code] ON [Coupons] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Coupons_CreatedByAccountId] ON [Coupons] ([CreatedByAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Coupons_IsActive] ON [Coupons] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CouponShops_CouponId_ShopId] ON [CouponShops] ([CouponId], [ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CouponShops_ShopId] ON [CouponShops] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CouponUsages_CouponId] ON [CouponUsages] ([CouponId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CouponUsages_OrderRequestId] ON [CouponUsages] ([OrderRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CouponUsages_UserId] ON [CouponUsages] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Discounts_IsActive] ON [Discounts] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Discounts_ProductId] ON [Discounts] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Discounts_ScopeType_ScopeId] ON [Discounts] ([ScopeType], [ScopeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FlashSaleProducts_FlashSaleId_ProductId] ON [FlashSaleProducts] ([FlashSaleId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FlashSaleProducts_ProductId] ON [FlashSaleProducts] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FlashSales_IsActive] ON [FlashSales] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FlashSales_StartDate_EndDate] ON [FlashSales] ([StartDate], [EndDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_HomepageBanners_IsActive] ON [HomepageBanners] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_HomepageBanners_SortOrder] ON [HomepageBanners] ([SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_CreatedAt] ON [InventoryTransactions] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_CreatedByAccountId] ON [InventoryTransactions] ([CreatedByAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_ProductVariantId] ON [InventoryTransactions] ([ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_InventoryTransactions_ReferenceType_ReferenceId] ON [InventoryTransactions] ([ReferenceType], [ReferenceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LocationLogs_AccountId] ON [LocationLogs] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LocationLogs_CountryCode] ON [LocationLogs] ([CountryCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LocationLogs_IpAddress] ON [LocationLogs] ([IpAddress]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LocationLogs_RecordedAt] ON [LocationLogs] ([RecordedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_AccountId] ON [Notifications] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_AccountId_IsRead] ON [Notifications] ([AccountId], [IsRead]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_CreatedAt] ON [Notifications] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderRequestItems_OrderRequestId] ON [OrderRequestItems] ([OrderRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderRequestItems_ProductVariantId] ON [OrderRequestItems] ([ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderRequestItems_SellerOrderId] ON [OrderRequestItems] ([SellerOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderRequests_AffiliateId] ON [OrderRequests] ([AffiliateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderRequests_Status] ON [OrderRequests] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderRequests_UserId] ON [OrderRequests] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistories_ChangedAt] ON [OrderStatusHistories] ([ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistories_ChangedByAccountId] ON [OrderStatusHistories] ([ChangedByAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistories_OrderRequestId] ON [OrderStatusHistories] ([OrderRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Partners_AccountId] ON [Partners] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Partners_AddedBySuperAdminId] ON [Partners] ([AddedBySuperAdminId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentAttempts_PaymentId] ON [PaymentAttempts] ([PaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_OrderRequestId] ON [Payments] ([OrderRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_Provider_ProviderTransactionId] ON [Payments] ([Provider], [ProviderTransactionId]) WHERE [ProviderTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_Status] ON [Payments] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductQuestions_AnsweredByShopkeeperId] ON [ProductQuestions] ([AnsweredByShopkeeperId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductQuestions_ProductId] ON [ProductQuestions] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductQuestions_ProductId_IsApproved] ON [ProductQuestions] ([ProductId], [IsApproved]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductQuestions_UserId] ON [ProductQuestions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_BrandId] ON [Products] ([BrandId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_IsActive] ON [Products] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_IsApproved] ON [Products] ([IsApproved]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_IsFeatured] ON [Products] ([IsFeatured]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_ShopId] ON [Products] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_Slug] ON [Products] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductVariants_IsActive] ON [ProductVariants] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductVariants_ProductId] ON [ProductVariants] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductVariants_Sku] ON [ProductVariants] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_AccountId] ON [RefreshTokens] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_FamilyId] ON [RefreshTokens] ([FamilyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_ReplacedByTokenId] ON [RefreshTokens] ([ReplacedByTokenId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Refunds_PaymentId] ON [Refunds] ([PaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Refunds_ProcessedByAccountId] ON [Refunds] ([ProcessedByAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Refunds_ReturnRequestId] ON [Refunds] ([ReturnRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Refunds_Status] ON [Refunds] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReturnItems_OrderRequestItemId] ON [ReturnItems] ([OrderRequestItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReturnItems_ReturnRequestId] ON [ReturnItems] ([ReturnRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReturnRequests_ProcessedByAccountId] ON [ReturnRequests] ([ProcessedByAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReturnRequests_SellerOrderId] ON [ReturnRequests] ([SellerOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReturnRequests_Status] ON [ReturnRequests] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReturnRequests_UserId] ON [ReturnRequests] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_OrderRequestItemId] ON [Reviews] ([OrderRequestItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_ProductId] ON [Reviews] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_UserId] ON [Reviews] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Reviews_UserId_OrderRequestItemId] ON [Reviews] ([UserId], [OrderRequestItemId]) WHERE [OrderRequestItemId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SellerEarnings_CommissionRuleId] ON [SellerEarnings] ([CommissionRuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SellerEarnings_SellerOrderId] ON [SellerEarnings] ([SellerOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SellerEarnings_Status] ON [SellerEarnings] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SellerOrders_OrderRequestId] ON [SellerOrders] ([OrderRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SellerOrders_ShopId] ON [SellerOrders] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SellerOrders_Status] ON [SellerOrders] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Settings_IsActive] ON [Settings] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Settings_Key] ON [Settings] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Shipments_SellerOrderId] ON [Shipments] ([SellerOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_ShippingMethodId] ON [Shipments] ([ShippingMethodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_Status] ON [Shipments] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_TrackingNumber] ON [Shipments] ([TrackingNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShippingMethods_IsActive] ON [ShippingMethods] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShippingMethods_ShopId] ON [ShippingMethods] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShippingMethods_ZoneId] ON [ShippingMethods] ([ZoneId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShopImages_ShopId] ON [ShopImages] ([ShopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Shopkeepers_AccountId] ON [Shopkeepers] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shopkeepers_ApprovedByAccountId] ON [Shopkeepers] ([ApprovedByAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shops_IsActive] ON [Shops] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shops_ShopkeeperId] ON [Shops] ([ShopkeeperId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Shops_Slug] ON [Shops] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SuperAdmins_AccountId] ON [SuperAdmins] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_AccountId] ON [Users] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_VariantAttributeValues_ProductVariantId] ON [VariantAttributeValues] ([ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VariantAttributeValues_ProductVariantId_AttributeName] ON [VariantAttributeValues] ([ProductVariantId], [AttributeName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_VerificationTokens_AccountId] ON [VerificationTokens] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_VerificationTokens_AccountId_Purpose] ON [VerificationTokens] ([AccountId], [Purpose]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Wallets_AccountId] ON [Wallets] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WalletTransactions_CreatedAt] ON [WalletTransactions] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WalletTransactions_ReferenceType_ReferenceId] ON [WalletTransactions] ([ReferenceType], [ReferenceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WalletTransactions_WalletId] ON [WalletTransactions] ([WalletId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Wishlists_ProductId] ON [Wishlists] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Wishlists_UserId_ProductId] ON [Wishlists] ([UserId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Withdrawals_AccountId] ON [Withdrawals] ([AccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Withdrawals_RequestedAt] ON [Withdrawals] ([RequestedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Withdrawals_Status] ON [Withdrawals] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260922073226_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260922073226_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926091938_AddHomepageCategoryFields'
)
BEGIN
    ALTER TABLE [Categories] ADD [HomepageDisplayOrder] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926091938_AddHomepageCategoryFields'
)
BEGIN
    ALTER TABLE [Categories] ADD [IsFeaturedOnHomepage] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926091938_AddHomepageCategoryFields'
)
BEGIN
    CREATE INDEX [IX_Categories_IsFeaturedOnHomepage_HomepageDisplayOrder] ON [Categories] ([IsFeaturedOnHomepage], [HomepageDisplayOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926091938_AddHomepageCategoryFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260926091938_AddHomepageCategoryFields', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927185524_AddShippingAddressToOrderRequest'
)
BEGIN
    ALTER TABLE [OrderRequests] ADD [CustomerName] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927185524_AddShippingAddressToOrderRequest'
)
BEGIN
    ALTER TABLE [OrderRequests] ADD [CustomerPhone] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927185524_AddShippingAddressToOrderRequest'
)
BEGIN
    ALTER TABLE [OrderRequests] ADD [ShippingAddress] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927185524_AddShippingAddressToOrderRequest'
)
BEGIN
    ALTER TABLE [OrderRequests] ADD [City] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927185524_AddShippingAddressToOrderRequest'
)
BEGIN
    ALTER TABLE [OrderRequests] ADD [State] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927185524_AddShippingAddressToOrderRequest'
)
BEGIN
    ALTER TABLE [OrderRequests] ADD [Country] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927185524_AddShippingAddressToOrderRequest'
)
BEGIN
    ALTER TABLE [OrderRequests] ADD [OrderNotes] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927185524_AddShippingAddressToOrderRequest'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260927185524_AddShippingAddressToOrderRequest', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003125426_dataBaseRegistrationUpdate'
)
BEGIN
    IF COL_LENGTH('Shops', 'CustomBadgeText') IS NULL
        ALTER TABLE [Shops] ADD [CustomBadgeText] nvarchar(max) NULL;

    IF COL_LENGTH('Shops', 'ShopCategoryId') IS NULL
        ALTER TABLE [Shops] ADD [ShopCategoryId] uniqueidentifier NULL;

    IF COL_LENGTH('Shopkeepers', 'CnicBackUrl') IS NULL
        ALTER TABLE [Shopkeepers] ADD [CnicBackUrl] nvarchar(max) NULL;

    IF COL_LENGTH('Shopkeepers', 'CnicFrontUrl') IS NULL
        ALTER TABLE [Shopkeepers] ADD [CnicFrontUrl] nvarchar(max) NULL;

    IF COL_LENGTH('Shopkeepers', 'DocumentNumber') IS NULL
        ALTER TABLE [Shopkeepers] ADD [DocumentNumber] nvarchar(max) NULL;

    IF COL_LENGTH('Shopkeepers', 'DocumentType') IS NULL
        ALTER TABLE [Shopkeepers] ADD [DocumentType] nvarchar(max) NULL;

    IF COL_LENGTH('Shopkeepers', 'DocumentUrl') IS NULL
        ALTER TABLE [Shopkeepers] ADD [DocumentUrl] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003125426_dataBaseRegistrationUpdate'
)
BEGIN
    IF OBJECT_ID(N'[ShopCategories]', N'U') IS NULL
    BEGIN
        CREATE TABLE [ShopCategories] (
            [Id] uniqueidentifier NOT NULL,
            [Name] nvarchar(max) NOT NULL,
            [Slug] nvarchar(max) NOT NULL,
            [Description] nvarchar(max) NULL,
            [BadgeText] nvarchar(max) NULL,
            [BadgeColor] nvarchar(max) NULL,
            [IconUrl] nvarchar(max) NULL,
            [DisplayOrder] int NOT NULL,
            [IsActive] bit NOT NULL,
            [DeletedAt] datetime2 NULL,
            [CreatedAt] datetime2 NOT NULL,
            [UpdatedAt] datetime2 NOT NULL,
            CONSTRAINT [PK_ShopCategories] PRIMARY KEY ([Id])
        );
    END

    IF OBJECT_ID(N'[TierUpgradeRequests]', N'U') IS NULL
    BEGIN
        CREATE TABLE [TierUpgradeRequests] (
            [Id] uniqueidentifier NOT NULL,
            [ShopId] uniqueidentifier NOT NULL,
            [ShopName] nvarchar(max) NOT NULL,
            [MerchantName] nvarchar(max) NOT NULL,
            [CurrentTier] nvarchar(max) NOT NULL,
            [RequestedTier] nvarchar(max) NOT NULL,
            [RequestedCategoryId] uniqueidentifier NULL,
            [BadgeText] nvarchar(max) NULL,
            [Price] decimal(18,2) NOT NULL,
            [PaymentMethod] nvarchar(max) NULL,
            [ReferenceNote] nvarchar(max) NULL,
            [ReceiptUrl] nvarchar(max) NULL,
            [Status] nvarchar(max) NOT NULL,
            [RequestedAt] datetime2 NOT NULL,
            [ReviewedAt] datetime2 NULL,
            [ReviewNotes] nvarchar(max) NULL,
            [CreatedAt] datetime2 NOT NULL,
            [UpdatedAt] datetime2 NOT NULL,
            CONSTRAINT [PK_TierUpgradeRequests] PRIMARY KEY ([Id])
        );
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003125426_dataBaseRegistrationUpdate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003125426_dataBaseRegistrationUpdate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134930_MarketplaceCatalogListingFlow'
)
BEGIN
    ALTER TABLE [ShopCategories] ADD [MaxProductListings] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134930_MarketplaceCatalogListingFlow'
)
BEGIN
    ALTER TABLE [ShopCategories] ADD [PriceUsd] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134930_MarketplaceCatalogListingFlow'
)
BEGIN
    ALTER TABLE [Products] ADD [SourceProductId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134930_MarketplaceCatalogListingFlow'
)
BEGIN
    ALTER TABLE [ProductImages] ADD [IsCover] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134930_MarketplaceCatalogListingFlow'
)
BEGIN
    CREATE INDEX [IX_Products_ShopId_SourceProductId] ON [Products] ([ShopId], [SourceProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134930_MarketplaceCatalogListingFlow'
)
BEGIN
    CREATE INDEX [IX_Products_SourceProductId] ON [Products] ([SourceProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134930_MarketplaceCatalogListingFlow'
)
BEGIN
    ALTER TABLE [Products] ADD CONSTRAINT [FK_Products_Products_SourceProductId] FOREIGN KEY ([SourceProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003134930_MarketplaceCatalogListingFlow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003134930_MarketplaceCatalogListingFlow', N'10.0.12');
END;

COMMIT;
GO

