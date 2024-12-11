using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class AddIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Nodes_MqttServer",
                table: "Nodes",
                column: "MqttServer");

            migrationBuilder.CreateIndex(
                name: "IX_NeighborInfos_DataSource",
                table: "NeighborInfos",
                column: "DataSource");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Nodes_MqttServer",
                table: "Nodes");

            migrationBuilder.DropIndex(
                name: "IX_NeighborInfos_DataSource",
                table: "NeighborInfos");
        }
    }
}
