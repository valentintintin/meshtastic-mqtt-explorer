using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class WebhookMqttServerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "MqttServer",
                table: "Webhooks",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                table: "Webhooks",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Accepted",
                schema: "router",
                table: "PacketActivities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                schema: "router",
                table: "PacketActivities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PacketId",
                schema: "router",
                table: "PacketActivities",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<List<string>>(
                name: "ReceiverIds",
                schema: "router",
                table: "PacketActivities",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<long>(
                name: "NodeId1",
                schema: "router",
                table: "NodeConfigurations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PacketActivities_PacketId",
                schema: "router",
                table: "PacketActivities",
                column: "PacketId",
                unique: true);

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

            migrationBuilder.AddForeignKey(
                name: "FK_PacketActivities_Packets_PacketId",
                schema: "router",
                table: "PacketActivities",
                column: "PacketId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NodeConfigurations_Nodes_NodeId1",
                schema: "router",
                table: "NodeConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_PacketActivities_Packets_PacketId",
                schema: "router",
                table: "PacketActivities");

            migrationBuilder.DropIndex(
                name: "IX_PacketActivities_PacketId",
                schema: "router",
                table: "PacketActivities");

            migrationBuilder.DropIndex(
                name: "IX_NodeConfigurations_NodeId1",
                schema: "router",
                table: "NodeConfigurations");

            migrationBuilder.DropColumn(
                name: "Accepted",
                schema: "router",
                table: "PacketActivities");

            migrationBuilder.DropColumn(
                name: "Comment",
                schema: "router",
                table: "PacketActivities");

            migrationBuilder.DropColumn(
                name: "PacketId",
                schema: "router",
                table: "PacketActivities");

            migrationBuilder.DropColumn(
                name: "ReceiverIds",
                schema: "router",
                table: "PacketActivities");

            migrationBuilder.DropColumn(
                name: "NodeId1",
                schema: "router",
                table: "NodeConfigurations");

            migrationBuilder.AlterColumn<string>(
                name: "MqttServer",
                table: "Webhooks",
                type: "text",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Channel",
                table: "Webhooks",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);
        }
    }
}
