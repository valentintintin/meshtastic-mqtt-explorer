using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class DataPacketNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Packets_PacketId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_Positions_Packets_PacketId",
                table: "Positions");

            migrationBuilder.DropForeignKey(
                name: "FK_Telemetries_Packets_PacketId",
                table: "Telemetries");

            migrationBuilder.DropForeignKey(
                name: "FK_TextMessages_Packets_PacketId",
                table: "TextMessages");

            migrationBuilder.AlterColumn<long>(
                name: "PacketId",
                table: "TextMessages",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "PacketId",
                table: "Telemetries",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "PacketId",
                table: "Positions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "PacketId",
                table: "Packets",
                type: "numeric(20,0)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "PacketId",
                table: "NeighborInfos",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Packets_PacketId",
                table: "NeighborInfos",
                column: "PacketId",
                principalTable: "Packets",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Packets_PacketId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_Positions_Packets_PacketId",
                table: "Positions");

            migrationBuilder.DropForeignKey(
                name: "FK_Telemetries_Packets_PacketId",
                table: "Telemetries");

            migrationBuilder.DropForeignKey(
                name: "FK_TextMessages_Packets_PacketId",
                table: "TextMessages");

            migrationBuilder.AlterColumn<long>(
                name: "PacketId",
                table: "TextMessages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "PacketId",
                table: "Telemetries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "PacketId",
                table: "Positions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "PacketId",
                table: "Packets",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)");

            migrationBuilder.AlterColumn<long>(
                name: "PacketId",
                table: "NeighborInfos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Packets_PacketId",
                table: "NeighborInfos",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_TextMessages_Packets_PacketId",
                table: "TextMessages",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
