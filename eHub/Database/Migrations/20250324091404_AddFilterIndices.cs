using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eHub.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFilterIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Packet_Channel",
                table: "Packet");

            migrationBuilder.CreateIndex(
                name: "IX_Packet_Id_DateCreated",
                table: "Packet",
                columns: new[] { "Id", "DateCreated" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Packet_Id_DateCreated",
                table: "Packet");

            migrationBuilder.CreateIndex(
                name: "IX_Packet_Channel",
                table: "Packet",
                column: "Channel");
        }
    }
}
