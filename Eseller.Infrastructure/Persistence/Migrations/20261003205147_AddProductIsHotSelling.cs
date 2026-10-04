using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductIsHotSelling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Column may already exist from earlier manual/schema work — keep idempotent.
            migrationBuilder.Sql("""
                IF COL_LENGTH('Products', 'IsHotSelling') IS NULL
                    ALTER TABLE [Products] ADD [IsHotSelling] bit NOT NULL CONSTRAINT [DF_Products_IsHotSelling] DEFAULT CAST(0 AS bit);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Products_IsHotSelling'
                      AND object_id = OBJECT_ID(N'[Products]')
                )
                    CREATE INDEX [IX_Products_IsHotSelling] ON [Products] ([IsHotSelling]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Products_IsHotSelling'
                      AND object_id = OBJECT_ID(N'[Products]')
                )
                    DROP INDEX [IX_Products_IsHotSelling] ON [Products];

                IF COL_LENGTH('Products', 'IsHotSelling') IS NOT NULL
                    ALTER TABLE [Products] DROP COLUMN [IsHotSelling];
                """);
        }
    }
}
