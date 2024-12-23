using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class SignalHistoryPacket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PacketId",
                table: "SignalHistories",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignalHistories_PacketId",
                table: "SignalHistories",
                column: "PacketId");

            migrationBuilder.AddForeignKey(
                name: "FK_SignalHistories_Packets_PacketId",
                table: "SignalHistories",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SignalHistories_Packets_PacketId",
                table: "SignalHistories");

            migrationBuilder.DropIndex(
                name: "IX_SignalHistories_PacketId",
                table: "SignalHistories");

            migrationBuilder.DropColumn(
                name: "PacketId",
                table: "SignalHistories");
        }
    }
}
