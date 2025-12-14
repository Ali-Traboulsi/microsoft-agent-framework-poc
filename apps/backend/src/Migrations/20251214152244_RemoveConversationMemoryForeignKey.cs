using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentFrameworkQuickStart.Migrations
{
    /// <inheritdoc />
    public partial class RemoveConversationMemoryForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationMemoryEntries_Threads_ConversationId",
                table: "ConversationMemoryEntries");

            migrationBuilder.AddColumn<Guid>(
                name: "ThreadId",
                table: "ConversationMemoryEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemoryEntries_ThreadId",
                table: "ConversationMemoryEntries",
                column: "ThreadId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationMemoryEntries_Threads_ThreadId",
                table: "ConversationMemoryEntries",
                column: "ThreadId",
                principalTable: "Threads",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationMemoryEntries_Threads_ThreadId",
                table: "ConversationMemoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMemoryEntries_ThreadId",
                table: "ConversationMemoryEntries");

            migrationBuilder.DropColumn(
                name: "ThreadId",
                table: "ConversationMemoryEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationMemoryEntries_Threads_ConversationId",
                table: "ConversationMemoryEntries",
                column: "ConversationId",
                principalTable: "Threads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
