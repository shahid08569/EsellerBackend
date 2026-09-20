using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRejectionReasonToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Products");
        }
    }
}
