using Google.Protobuf;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class Sixth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TextMessages_CreatedAt",
                table: "TextMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_CreatedAt",
                table: "Telemetries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_CreatedAt",
                table: "Positions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_CreatedAt",
                table: "Packets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_MqttServer",
                table: "Packets",
                column: "MqttServer");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_PortNum",
                table: "Packets",
                column: "PortNum");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_CreatedAt",
                table: "Nodes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_HardwareModel",
                table: "Nodes",
                column: "HardwareModel");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_LastSeen",
                table: "Nodes",
                column: "LastSeen",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_LongName",
                table: "Nodes",
                column: "LongName");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_ModemPreset",
                table: "Nodes",
                column: "ModemPreset");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_NodeId",
                table: "Nodes",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_RegionCode",
                table: "Nodes",
                column: "RegionCode");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_ShortName",
                table: "Nodes",
                column: "ShortName");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_Name",
                table: "Channels",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TextMessages_CreatedAt",
                table: "TextMessages");

            migrationBuilder.DropIndex(
                name: "IX_Telemetries_CreatedAt",
                table: "Telemetries");

            migrationBuilder.DropIndex(
                name: "IX_Positions_CreatedAt",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_Packets_CreatedAt",
                table: "Packets");

            migrationBuilder.DropIndex(
                name: "IX_Packets_MqttServer",
                table: "Packets");

            migrationBuilder.DropIndex(
                name: "IX_Packets_PortNum",
                table: "Packets");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_CreatedAt",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_HardwareModel",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_LastSeen",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_LongName",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_ModemPreset",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_NodeId",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_RegionCode",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_ShortName",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_Channels_Name",
                table: "Channels");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Payload",
                table: "Packets",
                type: "bytea",
                nullable: true,
                oldClrType: typeof(ByteString),
                oldType: "smallint[]",
                oldNullable: true);
        }
    }
}
