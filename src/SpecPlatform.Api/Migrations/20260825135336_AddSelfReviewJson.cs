using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSelfReviewJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelfReviewJson",
                table: "SpecVersions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelfReviewJson",
                table: "SpecVersions");
        }
    }
}
