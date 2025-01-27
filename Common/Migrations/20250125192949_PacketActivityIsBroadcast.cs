using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class PacketActivityIsBroadcast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBroadcast",
                schema: "router",
                table: "PacketActivities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
            
            migrationBuilder.Sql("update \"router\".\"PacketActivities\" set \"IsBroadcast\" = \"ReceiverIds\" = '[]'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBroadcast",
                schema: "router",
                table: "PacketActivities");
        }
    }
}
