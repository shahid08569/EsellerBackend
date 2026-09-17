using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGeocodingFieldsToLocationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "LocationLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "LocationLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "LocationLogs",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocationLogs_CountryCode",
                table: "LocationLogs",
                column: "CountryCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LocationLogs_CountryCode",
                table: "LocationLogs");

            migrationBuilder.DropColumn(
                name: "City",
                table: "LocationLogs");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "LocationLogs");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "LocationLogs");
        }
    }
}
