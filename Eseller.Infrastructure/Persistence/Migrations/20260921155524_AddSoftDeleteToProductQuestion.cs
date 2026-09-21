using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToProductQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsApproved",
                table: "ProductQuestions",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ProductQuestions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByAccountId",
                table: "ProductQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProductQuestions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ProductQuestions_ProductId_IsApproved",
                table: "ProductQuestions",
                columns: new[] { "ProductId", "IsApproved" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductQuestions_ProductId_IsApproved",
                table: "ProductQuestions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ProductQuestions");

            migrationBuilder.DropColumn(
                name: "DeletedByAccountId",
                table: "ProductQuestions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProductQuestions");

            migrationBuilder.AlterColumn<bool>(
                name: "IsApproved",
                table: "ProductQuestions",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);
        }
    }
}
