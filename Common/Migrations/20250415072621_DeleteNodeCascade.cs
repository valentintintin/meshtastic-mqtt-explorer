using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class DeleteNodeCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Nodes_NextHopId",
                table: "Packets");

            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Nodes_RelayNodeId",
                table: "Packets");

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Nodes_NextHopId",
                table: "Packets",
                column: "NextHopId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Nodes_RelayNodeId",
                table: "Packets",
                column: "RelayNodeId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
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
    }
}
