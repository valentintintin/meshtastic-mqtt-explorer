using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class Waypoint2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "WaypointId",
                table: "Waypoints",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_WaypointId",
                table: "Waypoints",
                column: "WaypointId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Waypoints_WaypointId",
                table: "Waypoints");

            migrationBuilder.DropColumn(
                name: "WaypointId",
                table: "Waypoints");
        }
    }
}
