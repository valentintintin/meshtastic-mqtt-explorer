using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class Tenth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllNames",
                table: "Nodes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NodeIdString",
                table: "Nodes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_AllNames",
                table: "Nodes",
                column: "AllNames");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_NodeIdString",
                table: "Nodes",
                column: "NodeIdString");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Nodes_AllNames",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_NodeIdString",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "AllNames",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "NodeIdString",
                table: "Nodes");
        }
    }
}
