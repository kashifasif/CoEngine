using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecPlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIsUndoneToSpecVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUndone",
                table: "SpecVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsUndone",
                table: "SpecVersions");
        }
    }
}
