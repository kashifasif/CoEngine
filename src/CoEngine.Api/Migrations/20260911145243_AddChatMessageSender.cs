using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoEngine.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageSender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "ChatMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SenderUserId",
                table: "ChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SenderUserId",
                table: "ChatMessages",
                column: "SenderUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Users_SenderUserId",
                table: "ChatMessages",
                column: "SenderUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Users_SenderUserId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_SenderUserId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "SenderUserId",
                table: "ChatMessages");
        }
    }
}
