using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wealthra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateNotificationLanguages : Migration
    {
        /// <inheritdoc />
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

            // Copy data from old Message column
            migrationBuilder.Sql("UPDATE \"Notifications\" SET \"MessageEn\" = \"Message\", \"MessageTr\" = \"Message\"");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Notifications");

            // Make columns non-nullable after migration
            migrationBuilder.AlterColumn<string>(
                name: "MessageEn",
                table: "Notifications",
                type: "text",
                nullable: false,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MessageTr",
                table: "Notifications",
                type: "text",
                nullable: false,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"Notifications\" SET \"Message\" = COALESCE(\"MessageEn\", \"MessageTr\", '')");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "text",
                nullable: false,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "MessageEn",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "MessageTr",
                table: "Notifications");
        }
    }
}
