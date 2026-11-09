using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentKart.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewModerationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Reviews",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModeratedAtUtc",
                table: "Reviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationReason",
                table: "Reviews",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_IsHidden",
                table: "Reviews",
                column: "IsHidden");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_IsHidden",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ModeratedAtUtc",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ModerationReason",
                table: "Reviews");
        }
    }
}
