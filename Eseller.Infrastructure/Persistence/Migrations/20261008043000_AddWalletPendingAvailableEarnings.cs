using Eseller.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eseller.Infrastructure.Persistence.Migrations;

[DbContext(typeof(EsellerDbContext))]
[Migration("20261008043000_AddWalletPendingAvailableEarnings")]
public partial class AddWalletPendingAvailableEarnings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH(N'Wallets', N'PendingEarnings') IS NULL
BEGIN
    ALTER TABLE [Wallets] ADD [PendingEarnings] decimal(18,2) NOT NULL
        CONSTRAINT [DF_Wallets_PendingEarnings] DEFAULT 0;
END

IF COL_LENGTH(N'Wallets', N'AvailableEarnings') IS NULL
BEGIN
    ALTER TABLE [Wallets] ADD [AvailableEarnings] decimal(18,2) NOT NULL
        CONSTRAINT [DF_Wallets_AvailableEarnings] DEFAULT 0;
END

-- Backfill from SellerEarnings (Pending / Available) via Shop → Shopkeeper → Account
;WITH EarningTotals AS (
    SELECT
        sk.AccountId,
        SUM(CASE WHEN se.Status = 1 /* Pending */
            THEN CASE WHEN se.NetAmount > 0 THEN se.NetAmount ELSE se.CommissionAmount END
            ELSE 0 END) AS PendingTotal,
        SUM(CASE WHEN se.Status = 2 /* Available */
            THEN CASE WHEN se.NetAmount > 0 THEN se.NetAmount ELSE se.CommissionAmount END
            ELSE 0 END) AS AvailableTotal
    FROM SellerEarnings se
    INNER JOIN SellerOrders so ON so.Id = se.SellerOrderId
    INNER JOIN Shops s ON s.Id = so.ShopId AND s.DeletedAt IS NULL
    INNER JOIN Shopkeepers sk ON sk.Id = s.ShopkeeperId
    WHERE se.Status IN (1, 2)
    GROUP BY sk.AccountId
)
UPDATE w
SET
    w.PendingEarnings = ISNULL(e.PendingTotal, 0),
    w.AvailableEarnings = ISNULL(e.AvailableTotal, 0)
FROM Wallets w
INNER JOIN EarningTotals e ON e.AccountId = w.AccountId;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH(N'Wallets', N'PendingEarnings') IS NOT NULL
    ALTER TABLE [Wallets] DROP CONSTRAINT [DF_Wallets_PendingEarnings];
IF COL_LENGTH(N'Wallets', N'PendingEarnings') IS NOT NULL
    ALTER TABLE [Wallets] DROP COLUMN [PendingEarnings];

IF COL_LENGTH(N'Wallets', N'AvailableEarnings') IS NOT NULL
    ALTER TABLE [Wallets] DROP CONSTRAINT [DF_Wallets_AvailableEarnings];
IF COL_LENGTH(N'Wallets', N'AvailableEarnings') IS NOT NULL
    ALTER TABLE [Wallets] DROP COLUMN [AvailableEarnings];
");
    }
}
