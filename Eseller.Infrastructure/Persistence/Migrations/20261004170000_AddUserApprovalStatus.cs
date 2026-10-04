using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddUserApprovalStatus : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing customers stay Approved so they can still order.
        // New registrations set Status = Pending (1) explicitly.
        migrationBuilder.Sql("""
            IF COL_LENGTH('Users', 'Status') IS NULL
                ALTER TABLE [Users] ADD [Status] int NOT NULL
                    CONSTRAINT [DF_Users_Status] DEFAULT 2;

            IF COL_LENGTH('Users', 'ApprovedByAccountId') IS NULL
                ALTER TABLE [Users] ADD [ApprovedByAccountId] uniqueidentifier NULL;

            IF COL_LENGTH('Users', 'ApprovedAt') IS NULL
                ALTER TABLE [Users] ADD [ApprovedAt] datetime2 NULL;

            IF COL_LENGTH('Users', 'RejectionReason') IS NULL
                ALTER TABLE [Users] ADD [RejectionReason] nvarchar(500) NULL;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Users_Status'
                  AND object_id = OBJECT_ID(N'[Users]')
            )
                CREATE INDEX [IX_Users_Status] ON [Users] ([Status]);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Users_Status'
                  AND object_id = OBJECT_ID(N'[Users]')
            )
                DROP INDEX [IX_Users_Status] ON [Users];

            IF COL_LENGTH('Users', 'RejectionReason') IS NOT NULL
                ALTER TABLE [Users] DROP COLUMN [RejectionReason];

            IF COL_LENGTH('Users', 'ApprovedAt') IS NOT NULL
                ALTER TABLE [Users] DROP COLUMN [ApprovedAt];

            IF COL_LENGTH('Users', 'ApprovedByAccountId') IS NOT NULL
                ALTER TABLE [Users] DROP COLUMN [ApprovedByAccountId];

            IF COL_LENGTH('Users', 'Status') IS NOT NULL
            BEGIN
                DECLARE @df sysname;
                SELECT @df = d.name
                FROM sys.default_constraints d
                INNER JOIN sys.columns c ON c.default_object_id = d.object_id
                WHERE d.parent_object_id = OBJECT_ID(N'[Users]') AND c.name = N'Status';
                IF @df IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @df + N']');
                ALTER TABLE [Users] DROP COLUMN [Status];
            END
            """);
    }
}
