using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eHub.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddedDynamicFieldColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DynamicField",
                table: "Packet",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DynamicField",
                table: "Packet");
        }
    }
}
