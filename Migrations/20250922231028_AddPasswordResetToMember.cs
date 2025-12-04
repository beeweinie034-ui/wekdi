using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wekdi.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetToMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetExpiry",
                table: "Members",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetToken",
                table: "Members",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordResetExpiry",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                table: "Members");
        }
    }
}
