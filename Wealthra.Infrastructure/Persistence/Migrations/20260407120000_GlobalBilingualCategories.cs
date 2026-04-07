using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using Wealthra.Domain.Constants;

#nullable disable

namespace Wealthra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GlobalBilingualCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP VIEW IF EXISTS "vw_MonthlyCategoryMetrics";""");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name_CreatedBy",
                table: "Categories");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameTr",
                table: "Categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""UPDATE "Categories" SET "NameEn" = "Name", "NameTr" = "Name";""");

            migrationBuilder.Sql("""
                UPDATE "Expenses" e SET "CategoryId" = r."KeepId"
                FROM (
                  SELECT "Id", MIN("Id") OVER (PARTITION BY "NameEn") AS "KeepId"
                  FROM "Categories"
                ) r
                WHERE e."CategoryId" = r."Id" AND r."Id" <> r."KeepId";

                UPDATE "Budgets" b SET "CategoryId" = r."KeepId"
                FROM (
                  SELECT "Id", MIN("Id") OVER (PARTITION BY "NameEn") AS "KeepId"
                  FROM "Categories"
                ) r
                WHERE b."CategoryId" = r."Id" AND r."Id" <> r."KeepId";

                DELETE FROM "Categories" c
                WHERE c."Id" IN (
                  SELECT x."Id" FROM (
                    SELECT "Id", MIN("Id") OVER (PARTITION BY "NameEn") AS keep_id
                    FROM "Categories"
                  ) x WHERE x."Id" <> x.keep_id
                );
                """);

            var caseParts = string.Join(" ",
                StandardGlobalCategoryPairs.Pairs.Select(p =>
                    $"WHEN '{p.NameEn.Replace("'", "''")}' THEN '{p.NameTr.Replace("'", "''")}'"));
            migrationBuilder.Sql(
                $"""UPDATE "Categories" SET "NameTr" = CASE "NameEn" {caseParts} ELSE "NameEn" END;""");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Categories");

            migrationBuilder.Sql(
                """ALTER TABLE "Categories" ALTER COLUMN "NameEn" SET NOT NULL;""");
            migrationBuilder.Sql(
                """ALTER TABLE "Categories" ALTER COLUMN "NameTr" SET NOT NULL;""");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_NameEn",
                table: "Categories",
                column: "NameEn",
                unique: true);

            migrationBuilder.Sql("""
                CREATE OR REPLACE VIEW "vw_MonthlyCategoryMetrics" AS
                WITH MonthlyIncome AS (
                    SELECT
                        "CreatedBy" AS "UserId",
                        DATE_TRUNC('month', "TransactionDate") AS "Month",
                        SUM("Amount") AS "TotalIncome"
                    FROM "Incomes"
                    GROUP BY "CreatedBy", DATE_TRUNC('month', "TransactionDate")
                ),
                MonthlySpend AS (
                    SELECT
                        e."CreatedBy" AS "UserId",
                        DATE_TRUNC('month', e."TransactionDate") AS "Month",
                        e."CategoryId",
                        c."NameEn" AS "CategoryName",
                        c."NameTr" AS "CategoryNameTr",
                        SUM(e."Amount") AS "TotalSpend"
                    FROM "Expenses" e
                    JOIN "Categories" c ON e."CategoryId" = c."Id"
                    GROUP BY e."CreatedBy", DATE_TRUNC('month', e."TransactionDate"), e."CategoryId", c."NameEn", c."NameTr"
                )
                SELECT
                    ms."UserId",
                    ms."Month",
                    ms."CategoryId",
                    ms."CategoryName",
                    ms."CategoryNameTr",
                    ms."TotalSpend",
                    COALESCE(mi."TotalIncome", 0) AS "TotalIncome",
                    CASE
                        WHEN COALESCE(mi."TotalIncome", 0) > 0 THEN (ms."TotalSpend" / mi."TotalIncome") * 100
                        ELSE 0
                    END AS "SpendPercentageOfIncome",
                    COALESCE(
                        LAG(ms."TotalSpend") OVER (PARTITION BY ms."UserId", ms."CategoryId" ORDER BY ms."Month"),
                        0
                    ) AS "PreviousMonthSpend"
                FROM MonthlySpend ms
                LEFT JOIN MonthlyIncome mi ON ms."UserId" = mi."UserId" AND ms."Month" = mi."Month";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP VIEW IF EXISTS "vw_MonthlyCategoryMetrics";""");

            migrationBuilder.DropIndex(
                name: "IX_Categories_NameEn",
                table: "Categories");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""UPDATE "Categories" SET "Name" = "NameEn";""");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Categories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.DropColumn(name: "NameEn", table: "Categories");
            migrationBuilder.DropColumn(name: "NameTr", table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name_CreatedBy",
                table: "Categories",
                columns: new[] { "Name", "CreatedBy" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE OR REPLACE VIEW "vw_MonthlyCategoryMetrics" AS
                WITH MonthlyIncome AS (
                    SELECT
                        "CreatedBy" AS "UserId",
                        DATE_TRUNC('month', "TransactionDate") AS "Month",
                        SUM("Amount") AS "TotalIncome"
                    FROM "Incomes"
                    GROUP BY "CreatedBy", DATE_TRUNC('month', "TransactionDate")
                ),
                MonthlySpend AS (
                    SELECT
                        e."CreatedBy" AS "UserId",
                        DATE_TRUNC('month', e."TransactionDate") AS "Month",
                        e."CategoryId",
                        c."Name" AS "CategoryName",
                        SUM(e."Amount") AS "TotalSpend"
                    FROM "Expenses" e
                    JOIN "Categories" c ON e."CategoryId" = c."Id"
                    GROUP BY e."CreatedBy", DATE_TRUNC('month', e."TransactionDate"), e."CategoryId", c."Name"
                )
                SELECT
                    ms."UserId",
                    ms."Month",
                    ms."CategoryId",
                    ms."CategoryName",
                    ms."TotalSpend",
                    COALESCE(mi."TotalIncome", 0) AS "TotalIncome",
                    CASE
                        WHEN COALESCE(mi."TotalIncome", 0) > 0 THEN (ms."TotalSpend" / mi."TotalIncome") * 100
                        ELSE 0
                    END AS "SpendPercentageOfIncome",
                    COALESCE(
                        LAG(ms."TotalSpend") OVER (PARTITION BY ms."UserId", ms."CategoryId" ORDER BY ms."Month"),
                        0
                    ) AS "PreviousMonthSpend"
                FROM MonthlySpend ms
                LEFT JOIN MonthlyIncome mi ON ms."UserId" = mi."UserId" AND ms."Month" = mi."Month";
                """);
        }
    }
}
