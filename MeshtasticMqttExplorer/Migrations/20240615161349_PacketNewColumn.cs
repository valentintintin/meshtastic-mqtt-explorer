using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class PacketNewColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "GatewayPositionId",
                table: "Packets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "HopStart",
                table: "Packets",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packets_GatewayPositionId",
                table: "Packets",
                column: "GatewayPositionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Positions_GatewayPositionId",
                table: "Packets",
                column: "GatewayPositionId",
                principalTable: "Positions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Positions_GatewayPositionId",
                table: "Packets");

            migrationBuilder.DropIndex(
                name: "IX_Packets_GatewayPositionId",
                table: "Packets");

            migrationBuilder.DropColumn(
                name: "GatewayPositionId",
                table: "Packets");

            migrationBuilder.DropColumn(
                name: "HopStart",
                table: "Packets");
        }
    }
}
