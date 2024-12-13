using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class RouterNodeConfigurationNodeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NodeConfigurations_Nodes_NodeId1",
                schema: "router",
                table: "NodeConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_NodeConfigurations_NodeId1",
                schema: "router",
                table: "NodeConfigurations");

            migrationBuilder.DropColumn(
                name: "NodeId1",
                schema: "router",
                table: "NodeConfigurations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "NodeId1",
                schema: "router",
                table: "NodeConfigurations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NodeConfigurations_NodeId1",
                schema: "router",
                table: "NodeConfigurations",
                column: "NodeId1",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NodeConfigurations_Nodes_NodeId1",
                schema: "router",
                table: "NodeConfigurations",
                column: "NodeId1",
                principalTable: "Nodes",
                principalColumn: "Id");
        }
    }
}
