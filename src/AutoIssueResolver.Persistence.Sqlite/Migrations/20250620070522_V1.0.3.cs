using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoIssueResolver.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class V103 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "ApplicationRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Model",
                table: "ApplicationRuns");
        }
    }
}
