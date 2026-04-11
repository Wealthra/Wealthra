using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wealthra.Infrastructure.Persistence.Migrations
{
    public partial class BilingualNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MessageEn",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageTr",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""UPDATE \"Notifications\" SET \"MessageEn\" = \"Message\", \"MessageTr\" = \"Message\";""");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Notifications");

            migrationBuilder.Sql("""ALTER TABLE \"Notifications\" ALTER COLUMN \"MessageEn\" SET NOT NULL;""");
            migrationBuilder.Sql("""ALTER TABLE \"Notifications\" ALTER COLUMN \"MessageTr\" SET NOT NULL;""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""UPDATE \"Notifications\" SET \"Message\" = COALESCE(\"MessageEn\", \"MessageTr\", '');""");

            migrationBuilder.Sql("""ALTER TABLE \"Notifications\" ALTER COLUMN \"Message\" SET NOT NULL;""");

            migrationBuilder.DropColumn(
                name: "MessageEn",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "MessageTr",
                table: "Notifications");
        }
    }
}
