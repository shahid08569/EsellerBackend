using Eseller.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations;

/// <summary>
/// Legacy migration retained for history. Columns superseded by RebuildNotificationsArchitecture.
/// </summary>
[DbContext(typeof(EsellerDbContext))]
[Migration("20261005080000_AddNotificationLinkPath")]
public partial class AddNotificationLinkPath : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No-op: LinkPath/RelatedEntityId replaced by NavigationUrl/EntityId in RebuildNotificationsArchitecture.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
