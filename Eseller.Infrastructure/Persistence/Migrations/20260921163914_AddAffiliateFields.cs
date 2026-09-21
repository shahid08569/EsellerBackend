using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AffiliateClicks_Products_ProductId",
                table: "AffiliateClicks");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Affiliates",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalClicks",
                table: "Affiliates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalConversions",
                table: "Affiliates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalEarned",
                table: "Affiliates",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "AffiliateCommissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderRequestId",
                table: "AffiliateCommissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "AffiliateCommissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Converted",
                table: "AffiliateClicks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "AffiliateClicks",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissions_AffiliateId_Status",
                table: "AffiliateCommissions",
                columns: new[] { "AffiliateId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissions_OrderRequestId",
                table: "AffiliateCommissions",
                column: "OrderRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateClicks_AffiliateId_Converted",
                table: "AffiliateClicks",
                columns: new[] { "AffiliateId", "Converted" });

            migrationBuilder.AddForeignKey(
                name: "FK_AffiliateClicks_Products_ProductId",
                table: "AffiliateClicks",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AffiliateCommissions_OrderRequests_OrderRequestId",
                table: "AffiliateCommissions",
                column: "OrderRequestId",
                principalTable: "OrderRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AffiliateClicks_Products_ProductId",
                table: "AffiliateClicks");

            migrationBuilder.DropForeignKey(
                name: "FK_AffiliateCommissions_OrderRequests_OrderRequestId",
                table: "AffiliateCommissions");

            migrationBuilder.DropIndex(
                name: "IX_AffiliateCommissions_AffiliateId_Status",
                table: "AffiliateCommissions");

            migrationBuilder.DropIndex(
                name: "IX_AffiliateCommissions_OrderRequestId",
                table: "AffiliateCommissions");

            migrationBuilder.DropIndex(
                name: "IX_AffiliateClicks_AffiliateId_Converted",
                table: "AffiliateClicks");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "TotalClicks",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "TotalConversions",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "TotalEarned",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "AffiliateCommissions");

            migrationBuilder.DropColumn(
                name: "OrderRequestId",
                table: "AffiliateCommissions");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "AffiliateCommissions");

            migrationBuilder.DropColumn(
                name: "Converted",
                table: "AffiliateClicks");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "AffiliateClicks");

            migrationBuilder.AddForeignKey(
                name: "FK_AffiliateClicks_Products_ProductId",
                table: "AffiliateClicks",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
