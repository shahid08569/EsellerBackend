using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHomepageCategoryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HomepageDisplayOrder",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeaturedOnHomepage",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_IsFeaturedOnHomepage_HomepageDisplayOrder",
                table: "Categories",
                columns: new[] { "IsFeaturedOnHomepage", "HomepageDisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categories_IsFeaturedOnHomepage_HomepageDisplayOrder",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "HomepageDisplayOrder",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsFeaturedOnHomepage",
                table: "Categories");
        }
    }
}
