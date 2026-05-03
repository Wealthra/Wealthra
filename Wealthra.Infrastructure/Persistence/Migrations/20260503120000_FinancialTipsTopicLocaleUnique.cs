using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wealthra.Infrastructure.Persistence;

#nullable disable

namespace Wealthra.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260503120000_FinancialTipsTopicLocaleUnique")]
    public partial class FinancialTipsTopicLocaleUnique : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FinancialTips_Topic_Locale",
                table: "FinancialTips",
                columns: new[] { "Topic", "Locale" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FinancialTips_Topic_Locale",
                table: "FinancialTips");
        }
    }
}
