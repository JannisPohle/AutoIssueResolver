using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoIssueResolver.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class V104 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestType",
                table: "Requests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RequestType",
                table: "Requests",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
