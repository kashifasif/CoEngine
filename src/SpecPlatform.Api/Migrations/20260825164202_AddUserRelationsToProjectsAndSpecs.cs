using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRelationsToProjectsAndSpecs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "Specs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "Projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ChatSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specs_CreatedByUserId",
                table: "Specs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CreatedByUserId",
                table: "Projects",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_UserId",
                table: "ChatSessions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSessions_Users_UserId",
                table: "ChatSessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_CreatedByUserId",
                table: "Projects",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Specs_Users_CreatedByUserId",
                table: "Specs",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatSessions_Users_UserId",
                table: "ChatSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Users_CreatedByUserId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_Specs_Users_CreatedByUserId",
                table: "Specs");

            migrationBuilder.DropIndex(
                name: "IX_Specs_CreatedByUserId",
                table: "Specs");

            migrationBuilder.DropIndex(
                name: "IX_Projects_CreatedByUserId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_UserId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Specs");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ChatSessions");
        }
    }
}
