using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class MoveSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "PacketActivities",
                newName: "PacketActivities",
                newSchema: "router");

            migrationBuilder.RenameTable(
                name: "NodeConfigurations",
                newName: "NodeConfigurations",
                newSchema: "router");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "PacketActivities",
                schema: "router",
                newName: "PacketActivities");

            migrationBuilder.RenameTable(
                name: "NodeConfigurations",
                schema: "router",
                newName: "NodeConfigurations");
        }
    }
}
