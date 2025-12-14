using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentFrameworkQuickStart.Migrations
{
    /// <inheritdoc />
    public partial class AddLongTermMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    TopicsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EntitiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ActionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    DecisionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PendingFollowUpsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SubAgentsUsedJson = table.Column<string>(type: "TEXT", nullable: false),
                    SatisfactionIndicator = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    MultiModalContentJson = table.Column<string>(type: "TEXT", nullable: false),
                    KeywordsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ImportanceScore = table.Column<double>(type: "REAL", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TurnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationSummaries_Threads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "Threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserMemories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PreferredLanguage = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ExpertiseLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RiskTolerance = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FrequentAccountsJson = table.Column<string>(type: "TEXT", nullable: false),
                    FrequentPortfoliosJson = table.Column<string>(type: "TEXT", nullable: false),
                    FrequentFundsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PreferencesJson = table.Column<string>(type: "TEXT", nullable: false),
                    FactsJson = table.Column<string>(type: "TEXT", nullable: false),
                    InterestsJson = table.Column<string>(type: "TEXT", nullable: false),
                    TopicFrequencyJson = table.Column<string>(type: "TEXT", nullable: false),
                    CommunicationStyleJson = table.Column<string>(type: "TEXT", nullable: false),
                    TotalConversations = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalTurns = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMemories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_ConversationId",
                table: "ConversationSummaries",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_CreatedAt",
                table: "ConversationSummaries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_ImportanceScore",
                table: "ConversationSummaries",
                column: "ImportanceScore");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_StartedAt",
                table: "ConversationSummaries",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_ThreadId",
                table: "ConversationSummaries",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_UserId",
                table: "ConversationSummaries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_UpdatedAt",
                table: "UserMemories",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_UserId",
                table: "UserMemories",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationSummaries");

            migrationBuilder.DropTable(
                name: "UserMemories");
        }
    }
}
