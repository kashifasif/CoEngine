using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSelfReviewJsonToSpecVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All other tables already exist in the live database (schema was applied outside of EF migrations).
            // This migration ONLY adds the new SelfReviewJson audit column to SpecVersions.
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
