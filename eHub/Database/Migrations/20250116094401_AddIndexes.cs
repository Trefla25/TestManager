using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eHub.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Packet_Channel",
                table: "Packet",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_Packet_Channel_Status",
                table: "Packet",
                columns: new[] { "Channel", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Packet_ParentId",
                table: "Packet",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Packet_Status",
                table: "Packet",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Packet_Channel",
                table: "Packet");

            migrationBuilder.DropIndex(
                name: "IX_Packet_Channel_Status",
                table: "Packet");

            migrationBuilder.DropIndex(
                name: "IX_Packet_ParentId",
                table: "Packet");

            migrationBuilder.DropIndex(
                name: "IX_Packet_Status",
                table: "Packet");
        }
    }
}
