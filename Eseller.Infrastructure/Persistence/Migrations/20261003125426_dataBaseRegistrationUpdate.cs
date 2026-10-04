using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class dataBaseRegistrationUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Shop document / badge fields (skip if already applied manually)
            migrationBuilder.Sql("""
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
                """);

            // OrderRequests address fields already added by AddShippingAddressToOrderRequest —
            // do not re-add them here.

            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[TierUpgradeRequests]', N'U') IS NOT NULL
                    DROP TABLE [TierUpgradeRequests];

                IF OBJECT_ID(N'[ShopCategories]', N'U') IS NOT NULL
                    DROP TABLE [ShopCategories];

                IF COL_LENGTH('Shops', 'CustomBadgeText') IS NOT NULL
                    ALTER TABLE [Shops] DROP COLUMN [CustomBadgeText];

                IF COL_LENGTH('Shops', 'ShopCategoryId') IS NOT NULL
                    ALTER TABLE [Shops] DROP COLUMN [ShopCategoryId];

                IF COL_LENGTH('Shopkeepers', 'CnicBackUrl') IS NOT NULL
                    ALTER TABLE [Shopkeepers] DROP COLUMN [CnicBackUrl];

                IF COL_LENGTH('Shopkeepers', 'CnicFrontUrl') IS NOT NULL
                    ALTER TABLE [Shopkeepers] DROP COLUMN [CnicFrontUrl];

                IF COL_LENGTH('Shopkeepers', 'DocumentNumber') IS NOT NULL
                    ALTER TABLE [Shopkeepers] DROP COLUMN [DocumentNumber];

                IF COL_LENGTH('Shopkeepers', 'DocumentType') IS NOT NULL
                    ALTER TABLE [Shopkeepers] DROP COLUMN [DocumentType];

                IF COL_LENGTH('Shopkeepers', 'DocumentUrl') IS NOT NULL
                    ALTER TABLE [Shopkeepers] DROP COLUMN [DocumentUrl];
                """);
        }
    }
}
