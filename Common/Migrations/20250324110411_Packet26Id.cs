using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class Packet26Id : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "NextHopId",
                table: "Packets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RelayNodeId",
                table: "Packets",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packets_NextHopId",
                table: "Packets",
                column: "NextHopId");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_RelayNodeId",
                table: "Packets",
                column: "RelayNodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Nodes_NextHopId",
                table: "Packets",
                column: "NextHopId",
                principalTable: "Nodes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Nodes_RelayNodeId",
                table: "Packets",
                column: "RelayNodeId",
                principalTable: "Nodes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Nodes_NextHopId",
                table: "Packets");

            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Nodes_RelayNodeId",
                table: "Packets");

            migrationBuilder.DropIndex(
                name: "IX_Packets_NextHopId",
                table: "Packets");

            migrationBuilder.DropIndex(
                name: "IX_Packets_RelayNodeId",
                table: "Packets");

            migrationBuilder.DropColumn(
                name: "NextHopId",
                table: "Packets");

            migrationBuilder.DropColumn(
                name: "RelayNodeId",
                table: "Packets");
        }
    }
}
