using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Wealthra.Infrastructure.Persistence.Migrations
{
    public partial class AddSubscriptionPlans : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MonthlyOcrLimit = table.Column<int>(type: "integer", nullable: false),
                    MonthlySttLimit = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_Name",
                table: "SubscriptionPlans",
                column: "Name",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionPlanId",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SubscriptionPlanId",
                table: "AspNetUsers",
                column: "SubscriptionPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_SubscriptionPlans_SubscriptionPlanId",
                table: "AspNetUsers",
                column: "SubscriptionPlanId",
                principalTable: "SubscriptionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(@"
INSERT INTO ""SubscriptionPlans"" (""Name"", ""Description"", ""MonthlyOcrLimit"", ""MonthlySttLimit"", ""IsActive"", ""CreatedOn"")
VALUES
('Free', 'Default free plan', 0, 0, TRUE, NOW()),
('Basic', 'Default basic plan', 40, 30, TRUE, NOW()),
('Limitless', 'Default limitless plan', 2147483647, 2147483647, TRUE, NOW());
");

            migrationBuilder.Sql(@"
UPDATE ""AspNetUsers"" u
SET ""SubscriptionPlanId"" = p.""Id""
FROM ""SubscriptionPlans"" p
WHERE
    (u.""SubscriptionTier"" = 1 AND p.""Name"" = 'Free')
    OR (u.""SubscriptionTier"" = 2 AND p.""Name"" = 'Basic')
    OR (u.""SubscriptionTier"" = 3 AND p.""Name"" = 'Limitless');
");

            migrationBuilder.Sql(@"
UPDATE ""AspNetUsers"" u
SET ""SubscriptionPlanId"" = p.""Id""
FROM ""SubscriptionPlans"" p
WHERE u.""SubscriptionPlanId"" IS NULL AND p.""Name"" = 'Basic';
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_SubscriptionPlans_SubscriptionPlanId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SubscriptionPlanId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlanId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");
        }
    }
}
