using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wealthra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSubscriptionTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsageActivityDate",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OcrRequestsThisMonth",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SttRequestsThisMonth",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionTier",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastUsageActivityDate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OcrRequestsThisMonth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SttRequestsThisMonth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SubscriptionTier",
                table: "AspNetUsers");
        }
    }
}
