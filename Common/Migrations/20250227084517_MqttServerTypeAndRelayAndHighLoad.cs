using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class MqttServerTypeAndRelayAndHighLoad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHighLoad",
                table: "MqttServers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "RelayPositionPrecision",
                table: "MqttServers",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "MqttServers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHighLoad",
                table: "MqttServers");

            migrationBuilder.DropColumn(
                name: "RelayPositionPrecision",
                table: "MqttServers");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "MqttServers");
        }
    }
}
