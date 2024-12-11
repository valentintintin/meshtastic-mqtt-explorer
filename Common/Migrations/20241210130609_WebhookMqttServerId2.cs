using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class WebhookMqttServerId2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MqttServer",
                table: "Webhooks",
                newName: "MqttServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Webhooks_MqttServerId",
                table: "Webhooks",
                column: "MqttServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Webhooks_MqttServers_MqttServerId",
                table: "Webhooks",
                column: "MqttServerId",
                principalTable: "MqttServers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Webhooks_MqttServers_MqttServerId",
                table: "Webhooks");

            migrationBuilder.DropIndex(
                name: "IX_Webhooks_MqttServerId",
                table: "Webhooks");

            migrationBuilder.RenameColumn(
                name: "MqttServerId",
                table: "Webhooks",
                newName: "MqttServer");
        }
    }
}
