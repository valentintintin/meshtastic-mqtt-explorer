using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class TracerouteFromTo2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Traceroutes_Packets_PacketId",
                table: "Traceroutes");

            migrationBuilder.AddColumn<long>(
                name: "NodeFromId",
                table: "Traceroutes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "NodeToId",
                table: "Traceroutes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_CreatedAt",
                table: "Traceroutes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_NodeFromId",
                table: "Traceroutes",
                column: "NodeFromId");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_NodeToId",
                table: "Traceroutes",
                column: "NodeToId");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_UpdatedAt",
                table: "Traceroutes",
                column: "UpdatedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_Traceroutes_Nodes_NodeFromId",
                table: "Traceroutes",
                column: "NodeFromId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Traceroutes_Nodes_NodeToId",
                table: "Traceroutes",
                column: "NodeToId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Traceroutes_Packets_PacketId",
                table: "Traceroutes",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Traceroutes_Nodes_NodeFromId",
                table: "Traceroutes");

            migrationBuilder.DropForeignKey(
                name: "FK_Traceroutes_Nodes_NodeToId",
                table: "Traceroutes");

            migrationBuilder.DropForeignKey(
                name: "FK_Traceroutes_Packets_PacketId",
                table: "Traceroutes");

            migrationBuilder.DropIndex(
                name: "IX_Traceroutes_CreatedAt",
                table: "Traceroutes");

            migrationBuilder.DropIndex(
                name: "IX_Traceroutes_NodeFromId",
                table: "Traceroutes");

            migrationBuilder.DropIndex(
                name: "IX_Traceroutes_NodeToId",
                table: "Traceroutes");

            migrationBuilder.DropIndex(
                name: "IX_Traceroutes_UpdatedAt",
                table: "Traceroutes");

            migrationBuilder.DropColumn(
                name: "NodeFromId",
                table: "Traceroutes");

            migrationBuilder.DropColumn(
                name: "NodeToId",
                table: "Traceroutes");

            migrationBuilder.AddForeignKey(
                name: "FK_Traceroutes_Packets_PacketId",
                table: "Traceroutes",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id");
        }
    }
}
