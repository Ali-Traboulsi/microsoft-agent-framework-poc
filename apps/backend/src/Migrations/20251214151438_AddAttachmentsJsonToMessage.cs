using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentFrameworkQuickStart.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentsJsonToMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentsJson",
                table: "Messages",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentsJson",
                table: "Messages");
        }
    }
}
