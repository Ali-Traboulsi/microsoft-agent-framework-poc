using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentFrameworkQuickStart.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationMemoryEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Threads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Threads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMemoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UserRequest = table.Column<string>(type: "TEXT", nullable: false),
                    AgentResponse = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMemoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMemoryEntries_Threads_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SubAgentName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ToolCallsJson = table.Column<string>(type: "TEXT", nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Threads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "Threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemoryEntries_ConversationId",
                table: "ConversationMemoryEntries",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemoryEntries_ConversationId_SequenceNumber",
                table: "ConversationMemoryEntries",
                columns: new[] { "ConversationId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemoryEntries_Timestamp",
                table: "ConversationMemoryEntries",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SequenceNumber",
                table: "Messages",
                column: "SequenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ThreadId",
                table: "Messages",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ThreadId_SequenceNumber",
                table: "Messages",
                columns: new[] { "ThreadId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Timestamp",
                table: "Messages",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Threads_CreatedAt",
                table: "Threads",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Threads_IsArchived",
                table: "Threads",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Threads_UpdatedAt",
                table: "Threads",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationMemoryEntries");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Threads");
        }
    }
}
