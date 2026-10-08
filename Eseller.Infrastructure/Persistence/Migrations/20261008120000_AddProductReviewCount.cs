using Eseller.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations;

[DbContext(typeof(EsellerDbContext))]
[Migration("20261008120000_AddProductReviewCount")]
public partial class AddProductReviewCount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH(N'Products', N'ReviewCount') IS NULL
BEGIN
    ALTER TABLE [Products] ADD [ReviewCount] int NOT NULL
        CONSTRAINT [DF_Products_ReviewCount] DEFAULT 0;
END

-- Per-product approved review counts
;WITH ReviewTotals AS (
    SELECT ProductId, COUNT(*) AS Cnt
    FROM Reviews
    WHERE IsApproved = 1
    GROUP BY ProductId
)
UPDATE p
SET p.ReviewCount = rt.Cnt
FROM Products p
INNER JOIN ReviewTotals rt ON rt.ProductId = p.Id;

-- Propagate listing review stats onto catalog source products (AvgRating already synced this way)
;WITH ListingStats AS (
    SELECT
        SourceProductId AS CatalogId,
        MAX(ReviewCount) AS ReviewCount,
        MAX(AvgRating) AS AvgRating
    FROM Products
    WHERE SourceProductId IS NOT NULL
      AND ReviewCount > 0
    GROUP BY SourceProductId
)
UPDATE p
SET
    p.ReviewCount = CASE WHEN ls.ReviewCount > p.ReviewCount THEN ls.ReviewCount ELSE p.ReviewCount END,
    p.AvgRating = CASE
        WHEN p.AvgRating IS NULL AND ls.AvgRating IS NOT NULL THEN ls.AvgRating
        ELSE p.AvgRating
    END
FROM Products p
INNER JOIN ListingStats ls ON ls.CatalogId = p.Id
WHERE p.SourceProductId IS NULL;

-- Propagate catalog review stats onto seller listings that have none
;WITH CatalogStats AS (
    SELECT Id AS CatalogId, ReviewCount, AvgRating
    FROM Products
    WHERE SourceProductId IS NULL AND ReviewCount > 0
)
UPDATE p
SET
    p.ReviewCount = CASE WHEN p.ReviewCount = 0 THEN cs.ReviewCount ELSE p.ReviewCount END,
    p.AvgRating = CASE WHEN p.AvgRating IS NULL THEN cs.AvgRating ELSE p.AvgRating END
FROM Products p
INNER JOIN CatalogStats cs ON cs.CatalogId = p.SourceProductId
WHERE p.SourceProductId IS NOT NULL;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH(N'Products', N'ReviewCount') IS NOT NULL
BEGIN
    ALTER TABLE [Products] DROP CONSTRAINT [DF_Products_ReviewCount];
    ALTER TABLE [Products] DROP COLUMN [ReviewCount];
END
");
    }
}
