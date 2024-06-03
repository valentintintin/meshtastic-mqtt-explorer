using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class First2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Positions_Packets_NodeId",
                table: "Positions");

            migrationBuilder.DropForeignKey(
                name: "FK_Telemetries_Packets_NodeId",
                table: "Telemetries");

            migrationBuilder.DropForeignKey(
                name: "FK_TextMessages_Channels_NodeId",
                table: "TextMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_TextMessages_Packets_NodeId",
                table: "TextMessages");

            migrationBuilder.CreateIndex(
                name: "IX_TextMessages_ChannelId",
                table: "TextMessages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_TextMessages_PacketId",
                table: "TextMessages",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_PacketId",
                table: "Telemetries",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_PacketId",
                table: "Positions",
                column: "PacketId");

            migrationBuilder.AddForeignKey(
                name: "FK_Positions_Packets_PacketId",
                table: "Positions",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Telemetries_Packets_PacketId",
                table: "Telemetries",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TextMessages_Channels_ChannelId",
                table: "TextMessages",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TextMessages_Packets_PacketId",
                table: "TextMessages",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Positions_Packets_PacketId",
                table: "Positions");

            migrationBuilder.DropForeignKey(
                name: "FK_Telemetries_Packets_PacketId",
                table: "Telemetries");

            migrationBuilder.DropForeignKey(
                name: "FK_TextMessages_Channels_ChannelId",
                table: "TextMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_TextMessages_Packets_PacketId",
                table: "TextMessages");

            migrationBuilder.DropIndex(
                name: "IX_TextMessages_ChannelId",
                table: "TextMessages");

            migrationBuilder.DropIndex(
                name: "IX_TextMessages_PacketId",
                table: "TextMessages");

            migrationBuilder.DropIndex(
                name: "IX_Telemetries_PacketId",
                table: "Telemetries");

            migrationBuilder.DropIndex(
                name: "IX_Positions_PacketId",
                table: "Positions");

            migrationBuilder.AddForeignKey(
                name: "FK_Positions_Packets_NodeId",
                table: "Positions",
                column: "NodeId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Telemetries_Packets_NodeId",
                table: "Telemetries",
                column: "NodeId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TextMessages_Channels_NodeId",
                table: "TextMessages",
                column: "NodeId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TextMessages_Packets_NodeId",
                table: "TextMessages",
                column: "NodeId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
