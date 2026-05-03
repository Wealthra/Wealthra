using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wealthra.Infrastructure.Persistence;

#nullable disable

namespace Wealthra.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260503140000_FinancialTipsEmbeddingDefault")]
    public partial class FinancialTipsEmbeddingDefault : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Lets EF INSERT rows without embedding; seeder overwrites with CAST(... AS vector).
            migrationBuilder.Sql(
                """ALTER TABLE "FinancialTips" ALTER COLUMN "Embedding" SET DEFAULT '[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]'::vector;""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """ALTER TABLE "FinancialTips" ALTER COLUMN "Embedding" DROP DEFAULT;""");
        }
    }
}
