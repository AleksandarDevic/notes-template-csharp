using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddNoteAIFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "content_summary",
                schema: "public",
                table: "notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "key_points",
                schema: "public",
                table: "notes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "content_summary",
                schema: "public",
                table: "notes");

            migrationBuilder.DropColumn(
                name: "key_points",
                schema: "public",
                table: "notes");
        }
    }
}
