using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentFrameworkQuickStart.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationContexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EntitiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    GoalsJson = table.Column<string>(type: "TEXT", nullable: false),
                    DecisionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SubAgentFindingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PendingActionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ActiveConstraintsJson = table.Column<string>(type: "TEXT", nullable: false),
                    UserExpertiseLevel = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PreferredLanguage = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    InferredRiskTolerance = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TurnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationContexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationContexts_Threads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "Threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationContexts_ConversationId",
                table: "ConversationContexts",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationContexts_LastActivityAt",
                table: "ConversationContexts",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationContexts_ThreadId",
                table: "ConversationContexts",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationContexts_UserId",
                table: "ConversationContexts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationContexts");
        }
    }
}
