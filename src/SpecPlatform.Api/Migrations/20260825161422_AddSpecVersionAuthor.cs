using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecVersionAuthor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<int>(
                name: "AuthorUserId",
                table: "SpecVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorUsername",
                table: "SpecVersions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorAvatarUrl",
                table: "SpecVersions");

            migrationBuilder.DropColumn(
                name: "AuthorDisplayName",
                table: "SpecVersions");

            migrationBuilder.DropColumn(
                name: "AuthorUserId",
                table: "SpecVersions");

            migrationBuilder.DropColumn(
                name: "AuthorUsername",
                table: "SpecVersions");
        }
    }
}
