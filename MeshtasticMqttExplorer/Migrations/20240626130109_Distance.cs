using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class Distance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "GatewayDistanceKm",
                table: "Packets",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PositionId",
                table: "Packets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Distance",
                table: "NeighborInfos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NeighborPositionId",
                table: "NeighborInfos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NodePositionId",
                table: "NeighborInfos",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packets_PositionId",
                table: "Packets",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_NeighborPositionId",
                table: "NeighborInfos",
                column: "NeighborPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_NodePositionId",
                table: "NeighborInfos",
                column: "NodePositionId");

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Positions_NeighborPositionId",
                table: "NeighborInfos",
                column: "NeighborPositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Positions_NodePositionId",
                table: "NeighborInfos",
                column: "NodePositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Positions_PositionId",
                table: "Packets",
                column: "PositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Positions_NeighborPositionId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Positions_NodePositionId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Positions_PositionId",
                table: "Packets");

            migrationBuilder.DropIndex(
                name: "IX_Packets_PositionId",
                table: "Packets");

            migrationBuilder.DropIndex(
                name: "IX_NeighborInfos_NeighborPositionId",
                table: "NeighborInfos");

            migrationBuilder.DropIndex(
                name: "IX_NeighborInfos_NodePositionId",
                table: "NeighborInfos");

            migrationBuilder.DropColumn(
                name: "GatewayDistanceKm",
                table: "Packets");

            migrationBuilder.DropColumn(
                name: "PositionId",
                table: "Packets");

            migrationBuilder.DropColumn(
                name: "Distance",
                table: "NeighborInfos");

            migrationBuilder.DropColumn(
                name: "NeighborPositionId",
                table: "NeighborInfos");

            migrationBuilder.DropColumn(
                name: "NodePositionId",
                table: "NeighborInfos");
        }
    }
}
