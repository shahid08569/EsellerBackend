using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AffiliateId",
                table: "OrderRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferralCodeSnapshot",
                table: "OrderRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionRate",
                table: "Affiliates",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_OrderRequests_AffiliateId",
                table: "OrderRequests",
                column: "AffiliateId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderRequests_Affiliates_AffiliateId",
                table: "OrderRequests",
                column: "AffiliateId",
                principalTable: "Affiliates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderRequests_Affiliates_AffiliateId",
                table: "OrderRequests");

            migrationBuilder.DropIndex(
                name: "IX_OrderRequests_AffiliateId",
                table: "OrderRequests");

            migrationBuilder.DropColumn(
                name: "AffiliateId",
                table: "OrderRequests");

            migrationBuilder.DropColumn(
                name: "ReferralCodeSnapshot",
                table: "OrderRequests");

            migrationBuilder.DropColumn(
                name: "CommissionRate",
                table: "Affiliates");
        }
    }
}
