using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eHub.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPacketDateCreatedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Packet_DateCreated",
                table: "Packet",
                column: "DateCreated");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Packet_DateCreated",
                table: "Packet");
        }
    }
}
