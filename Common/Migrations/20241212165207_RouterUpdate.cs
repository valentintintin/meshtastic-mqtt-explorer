using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class RouterUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PacketActivities_NodeConfigurations_NodeConfigurationId",
                schema: "router",
                table: "PacketActivities");

            migrationBuilder.DropIndex(
                name: "IX_PacketActivities_NodeConfigurationId",
                schema: "router",
                table: "PacketActivities");

            migrationBuilder.DropColumn(
                name: "NodeConfigurationId",
                schema: "router",
                table: "PacketActivities");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                schema: "router",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverIds",
                schema: "router",
                table: "PacketActivities",
                type: "text",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]");

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                schema: "router",
                table: "PacketActivities",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                schema: "router",
                table: "Users");

            migrationBuilder.AlterColumn<List<string>>(
                name: "ReceiverIds",
                schema: "router",
                table: "PacketActivities",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                schema: "router",
                table: "PacketActivities",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NodeConfigurationId",
                schema: "router",
                table: "PacketActivities",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_PacketActivities_NodeConfigurationId",
                schema: "router",
                table: "PacketActivities",
                column: "NodeConfigurationId");

            migrationBuilder.AddForeignKey(
                name: "FK_PacketActivities_NodeConfigurations_NodeConfigurationId",
                schema: "router",
                table: "PacketActivities",
                column: "NodeConfigurationId",
                principalSchema: "router",
                principalTable: "NodeConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
