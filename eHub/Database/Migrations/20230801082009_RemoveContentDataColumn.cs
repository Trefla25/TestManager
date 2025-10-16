using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eHub.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContentDataColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentData",
                table: "Packet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentData",
                table: "Packet",
                type: "TEXT",
                nullable: true);
        }
    }
}
