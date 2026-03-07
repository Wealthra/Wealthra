using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wealthra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyCategoryMetricsView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            CREATE OR REPLACE VIEW ""vw_MonthlyCategoryMetrics"" AS
            WITH MonthlyIncome AS (
                SELECT 
                    ""CreatedBy"" AS ""UserId"",
                    DATE_TRUNC('month', ""TransactionDate"") AS ""Month"",
                    SUM(""Amount"") AS ""TotalIncome""
                FROM ""Incomes""
                GROUP BY ""CreatedBy"", DATE_TRUNC('month', ""TransactionDate"")
            ),
            MonthlySpend AS (
                SELECT 
                    e.""CreatedBy"" AS ""UserId"",
                    DATE_TRUNC('month', e.""TransactionDate"") AS ""Month"",
                    e.""CategoryId"",
                    c.""Name"" AS ""CategoryName"",
                    SUM(e.""Amount"") AS ""TotalSpend""
                FROM ""Expenses"" e
                JOIN ""Categories"" c ON e.""CategoryId"" = c.""Id""
                GROUP BY e.""CreatedBy"", DATE_TRUNC('month', e.""TransactionDate""), e.""CategoryId"", c.""Name""
            )
            SELECT 
                ms.""UserId"",
                ms.""Month"",
                ms.""CategoryId"",
                ms.""CategoryName"",
                ms.""TotalSpend"",
                COALESCE(mi.""TotalIncome"", 0) AS ""TotalIncome"",
                CASE 
                    WHEN COALESCE(mi.""TotalIncome"", 0) > 0 THEN (ms.""TotalSpend"" / mi.""TotalIncome"") * 100
                    ELSE 0 
                END AS ""SpendPercentageOfIncome"",
                COALESCE(
                    LAG(ms.""TotalSpend"") OVER (PARTITION BY ms.""UserId"", ms.""CategoryId"" ORDER BY ms.""Month""), 
                    0
                ) AS ""PreviousMonthSpend""
            FROM MonthlySpend ms
            LEFT JOIN MonthlyIncome mi ON ms.""UserId"" = mi.""UserId"" AND ms.""Month"" = mi.""Month"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS \"vw_MonthlyCategoryMetrics\";");
        }
    }
}
