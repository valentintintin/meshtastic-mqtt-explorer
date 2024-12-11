using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMqttServerString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Packets_MqttServer",
                table: "Packets");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_MqttServer",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "MqttServer",
                table: "Packets");

            migrationBuilder.DropColumn(
                name: "MqttServer",
                table: "Nodes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MqttServer",
                table: "Packets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MqttServer",
                table: "Nodes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packets_MqttServer",
                table: "Packets",
                column: "MqttServer");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_MqttServer",
                table: "Nodes",
                column: "MqttServer");
        }
    }
}
