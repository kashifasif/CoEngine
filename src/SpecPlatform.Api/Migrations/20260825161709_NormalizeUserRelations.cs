using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUserRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorAvatarUrl",
                table: "SpecVersions");

            migrationBuilder.DropColumn(
                name: "AuthorDisplayName",
                table: "SpecVersions");

            migrationBuilder.DropColumn(
                name: "AuthorUsername",
                table: "SpecVersions");

            migrationBuilder.DropColumn(
                name: "AuthorAvatarUrl",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "AuthorDisplayName",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "AuthorUsername",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_UserAiUsages_UserId",
                table: "UserAiUsages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecVersions_AuthorUserId",
                table: "SpecVersions",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_AuthorUserId",
                table: "Notifications",
                column: "AuthorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_AuthorUserId",
                table: "Notifications",
                column: "AuthorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SpecVersions_Users_AuthorUserId",
                table: "SpecVersions",
                column: "AuthorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAiUsages_Users_UserId",
                table: "UserAiUsages",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_AuthorUserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_SpecVersions_Users_AuthorUserId",
                table: "SpecVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAiUsages_Users_UserId",
                table: "UserAiUsages");

            migrationBuilder.DropIndex(
                name: "IX_UserAiUsages_UserId",
                table: "UserAiUsages");

            migrationBuilder.DropIndex(
                name: "IX_SpecVersions_AuthorUserId",
                table: "SpecVersions");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_AuthorUserId",
                table: "Notifications");

            migrationBuilder.AddColumn<string>(
                name: "AuthorAvatarUrl",
                table: "SpecVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorDisplayName",
                table: "SpecVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorUsername",
                table: "SpecVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorAvatarUrl",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorDisplayName",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorUsername",
                table: "Notifications",
                type: "text",
                nullable: true);
        }
    }
}
