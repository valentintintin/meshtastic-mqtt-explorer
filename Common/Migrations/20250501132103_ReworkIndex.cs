using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class ReworkIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Waypoints_CreatedAt",
                table: "Waypoints");

            migrationBuilder.DropIndex(
                name: "IX_Waypoints_Name",
                table: "Waypoints");

            migrationBuilder.DropIndex(
                name: "IX_Telemetries_UpdatedAt",
                table: "Telemetries");

            migrationBuilder.DropIndex(
                name: "IX_Positions_UpdatedAt",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_PaxCounters_UpdatedAt",
                table: "PaxCounters");

            migrationBuilder.DropIndex(
                name: "IX_PacketActivities_Accepted",
                schema: "router",
                table: "PacketActivities");

            migrationBuilder.DropIndex(
                name: "IX_PacketActivities_CreatedAt",
                schema: "router",
                table: "PacketActivities");

            migrationBuilder.DropIndex(
                name: "IX_PacketActivities_UpdatedAt",
                schema: "router",
                table: "PacketActivities");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_HardwareModel",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_Latitude",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_Longitude",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_NodeConfigurations_CreatedAt",
                schema: "router",
                table: "NodeConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_NodeConfigurations_UpdatedAt",
                schema: "router",
                table: "NodeConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_NeighborInfos_CreatedAt",
                table: "NeighborInfos");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_UpdatedAt",
                table: "Positions",
                column: "UpdatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Channels_UpdatedAt",
                table: "Channels",
                column: "UpdatedAt",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Positions_UpdatedAt",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_Channels_UpdatedAt",
                table: "Channels");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_CreatedAt",
                table: "Waypoints",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_Name",
                table: "Waypoints",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_UpdatedAt",
                table: "Telemetries",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_UpdatedAt",
                table: "Positions",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaxCounters_UpdatedAt",
                table: "PaxCounters",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PacketActivities_Accepted",
                schema: "router",
                table: "PacketActivities",
                column: "Accepted");

            migrationBuilder.CreateIndex(
                name: "IX_PacketActivities_CreatedAt",
                schema: "router",
                table: "PacketActivities",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PacketActivities_UpdatedAt",
                schema: "router",
                table: "PacketActivities",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_HardwareModel",
                table: "Nodes",
                column: "HardwareModel");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_Latitude",
                table: "Nodes",
                column: "Latitude");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_Longitude",
                table: "Nodes",
                column: "Longitude");

            migrationBuilder.CreateIndex(
                name: "IX_NodeConfigurations_CreatedAt",
                schema: "router",
                table: "NodeConfigurations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NodeConfigurations_UpdatedAt",
                schema: "router",
                table: "NodeConfigurations",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_CreatedAt",
                table: "NeighborInfos",
                column: "CreatedAt");
        }
    }
}
