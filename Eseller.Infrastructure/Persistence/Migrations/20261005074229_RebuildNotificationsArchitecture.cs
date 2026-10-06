using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RebuildNotificationsArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[FK_Notifications_Accounts_AccountId]', N'F') IS NOT NULL
    ALTER TABLE [Notifications] DROP CONSTRAINT [FK_Notifications_Accounts_AccountId];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('Notifications', 'AccountId') IS NOT NULL AND COL_LENGTH('Notifications', 'RecipientUserId') IS NULL
    EXEC sp_rename N'[Notifications].[AccountId]', N'RecipientUserId', N'COLUMN';
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_AccountId_IsRead' AND object_id = OBJECT_ID(N'Notifications'))
    EXEC sp_rename N'[Notifications].[IX_Notifications_AccountId_IsRead]', N'IX_Notifications_RecipientUserId_IsRead', N'INDEX';
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_AccountId' AND object_id = OBJECT_ID(N'Notifications'))
    EXEC sp_rename N'[Notifications].[IX_Notifications_AccountId]', N'IX_Notifications_RecipientUserId', N'INDEX';
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('Notifications', 'LinkPath') IS NOT NULL
    ALTER TABLE [Notifications] DROP COLUMN [LinkPath];
IF COL_LENGTH('Notifications', 'RelatedEntityId') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_Type_RelatedEntityId_IsRead' AND object_id = OBJECT_ID(N'Notifications'))
        DROP INDEX [IX_Notifications_Type_RelatedEntityId_IsRead] ON [Notifications];
    ALTER TABLE [Notifications] DROP COLUMN [RelatedEntityId];
END
");

            migrationBuilder.Sql(@"
IF COL_LENGTH('Notifications', 'EntityId') IS NULL
    ALTER TABLE [Notifications] ADD [EntityId] uniqueidentifier NULL;
IF COL_LENGTH('Notifications', 'EntityType') IS NULL
    ALTER TABLE [Notifications] ADD [EntityType] int NOT NULL CONSTRAINT DF_Notifications_EntityType DEFAULT 0;
IF COL_LENGTH('Notifications', 'EventKey') IS NULL
    ALTER TABLE [Notifications] ADD [EventKey] nvarchar(200) NOT NULL CONSTRAINT DF_Notifications_EventKey DEFAULT '';
IF COL_LENGTH('Notifications', 'NavigationUrl') IS NULL
    ALTER TABLE [Notifications] ADD [NavigationUrl] nvarchar(500) NULL;
IF COL_LENGTH('Notifications', 'ReadAt') IS NULL
    ALTER TABLE [Notifications] ADD [ReadAt] datetime2 NULL;
IF COL_LENGTH('Notifications', 'RecipientRole') IS NULL
    ALTER TABLE [Notifications] ADD [RecipientRole] int NOT NULL CONSTRAINT DF_Notifications_RecipientRole DEFAULT 0;
");

            // Existing rows would collide on unique EventKey — clear legacy notifications
            migrationBuilder.Sql("DELETE FROM [Notifications];");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProductStockAlerts]', N'U') IS NULL
BEGIN
    CREATE TABLE [ProductStockAlerts] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [AlertType] nvarchar(50) NOT NULL,
        [TriggeredAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        CONSTRAINT [PK_ProductStockAlerts] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_ProductStockAlerts_ProductId] ON [ProductStockAlerts] ([ProductId]);
    CREATE INDEX [IX_ProductStockAlerts_ProductId_AlertType_ResolvedAt]
        ON [ProductStockAlerts] ([ProductId], [AlertType], [ResolvedAt]);
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_EventKey' AND object_id = OBJECT_ID(N'Notifications'))
    CREATE UNIQUE INDEX [IX_Notifications_EventKey] ON [Notifications] ([EventKey]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_Type_EntityId_RecipientUserId' AND object_id = OBJECT_ID(N'Notifications'))
    CREATE INDEX [IX_Notifications_Type_EntityId_RecipientUserId]
        ON [Notifications] ([Type], [EntityId], [RecipientUserId]);
IF OBJECT_ID(N'[FK_Notifications_Accounts_RecipientUserId]', N'F') IS NULL
    ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_Accounts_RecipientUserId]
        FOREIGN KEY ([RecipientUserId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[FK_Notifications_Accounts_RecipientUserId]', N'F') IS NOT NULL
    ALTER TABLE [Notifications] DROP CONSTRAINT [FK_Notifications_Accounts_RecipientUserId];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_EventKey' AND object_id = OBJECT_ID(N'Notifications'))
    DROP INDEX [IX_Notifications_EventKey] ON [Notifications];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_Type_EntityId_RecipientUserId' AND object_id = OBJECT_ID(N'Notifications'))
    DROP INDEX [IX_Notifications_Type_EntityId_RecipientUserId] ON [Notifications];
IF OBJECT_ID(N'[ProductStockAlerts]', N'U') IS NOT NULL
    DROP TABLE [ProductStockAlerts];
IF COL_LENGTH('Notifications', 'EntityId') IS NOT NULL ALTER TABLE [Notifications] DROP COLUMN [EntityId];
IF COL_LENGTH('Notifications', 'EntityType') IS NOT NULL ALTER TABLE [Notifications] DROP COLUMN [EntityType];
IF COL_LENGTH('Notifications', 'EventKey') IS NOT NULL ALTER TABLE [Notifications] DROP COLUMN [EventKey];
IF COL_LENGTH('Notifications', 'NavigationUrl') IS NOT NULL ALTER TABLE [Notifications] DROP COLUMN [NavigationUrl];
IF COL_LENGTH('Notifications', 'ReadAt') IS NOT NULL ALTER TABLE [Notifications] DROP COLUMN [ReadAt];
IF COL_LENGTH('Notifications', 'RecipientRole') IS NOT NULL ALTER TABLE [Notifications] DROP COLUMN [RecipientRole];
IF COL_LENGTH('Notifications', 'RecipientUserId') IS NOT NULL AND COL_LENGTH('Notifications', 'AccountId') IS NULL
    EXEC sp_rename N'[Notifications].[RecipientUserId]', N'AccountId', N'COLUMN';
IF OBJECT_ID(N'[FK_Notifications_Accounts_AccountId]', N'F') IS NULL AND COL_LENGTH('Notifications', 'AccountId') IS NOT NULL
    ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_Accounts_AccountId]
        FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE;
");
        }
    }
}
