using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexRouter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PacketActivities_Accepted",
                schema: "router",
                table: "PacketActivities",
                column: "Accepted");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_Latitude",
                table: "Nodes",
                column: "Latitude");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_Longitude",
                table: "Nodes",
                column: "Longitude");

            migrationBuilder.CreateIndex(
                name: "IX_NodeConfigurations_Department",
                schema: "router",
                table: "NodeConfigurations",
                column: "Department");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PacketActivities_Accepted",
                schema: "router",
                table: "PacketActivities");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_Latitude",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_Longitude",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_NodeConfigurations_Department",
                schema: "router",
                table: "NodeConfigurations");
        }
    }
}
