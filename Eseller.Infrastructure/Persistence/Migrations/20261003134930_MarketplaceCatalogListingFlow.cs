using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MarketplaceCatalogListingFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxProductListings",
                table: "ShopCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceUsd",
                table: "ShopCategories",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceProductId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCover",
                table: "ProductImages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Products_ShopId_SourceProductId",
                table: "Products",
                columns: new[] { "ShopId", "SourceProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_SourceProductId",
                table: "Products",
                column: "SourceProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Products_SourceProductId",
                table: "Products",
                column: "SourceProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Products_SourceProductId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_ShopId_SourceProductId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SourceProductId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MaxProductListings",
                table: "ShopCategories");

            migrationBuilder.DropColumn(
                name: "PriceUsd",
                table: "ShopCategories");

            migrationBuilder.DropColumn(
                name: "SourceProductId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsCover",
                table: "ProductImages");
        }
    }
}
