using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class Cascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Packets_PacketId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Positions_GatewayPositionId",
                table: "Packets");

            migrationBuilder.DropForeignKey(
                name: "FK_Positions_Packets_PacketId",
                table: "Positions");

            migrationBuilder.DropForeignKey(
                name: "FK_Telemetries_Packets_PacketId",
                table: "Telemetries");

            migrationBuilder.DropForeignKey(
                name: "FK_TextMessages_Packets_PacketId",
                table: "TextMessages");

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Packets_PacketId",
                table: "NeighborInfos",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Positions_GatewayPositionId",
                table: "Packets",
                column: "GatewayPositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Positions_Packets_PacketId",
                table: "Positions",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Telemetries_Packets_PacketId",
                table: "Telemetries",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TextMessages_Packets_PacketId",
                table: "TextMessages",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Packets_PacketId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Positions_GatewayPositionId",
                table: "Packets");

            migrationBuilder.DropForeignKey(
                name: "FK_Positions_Packets_PacketId",
                table: "Positions");

            migrationBuilder.DropForeignKey(
                name: "FK_Telemetries_Packets_PacketId",
                table: "Telemetries");

            migrationBuilder.DropForeignKey(
                name: "FK_TextMessages_Packets_PacketId",
                table: "TextMessages");

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Packets_PacketId",
                table: "NeighborInfos",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Positions_GatewayPositionId",
                table: "Packets",
                column: "GatewayPositionId",
                principalTable: "Positions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Positions_Packets_PacketId",
                table: "Positions",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Telemetries_Packets_PacketId",
                table: "Telemetries",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TextMessages_Packets_PacketId",
                table: "TextMessages",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id");
        }
    }
}
