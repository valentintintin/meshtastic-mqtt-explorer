using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class Config : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MqttServerId",
                table: "Packets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MqttServerId",
                table: "Nodes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MqttServers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Host = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Password = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Topics = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MqttServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Webhooks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    AllowDuplication = table.Column<bool>(type: "boolean", nullable: false),
                    AllowByHimSelf = table.Column<bool>(type: "boolean", nullable: false),
                    PortNum = table.Column<int>(type: "integer", nullable: true),
                    From = table.Column<long>(type: "bigint", nullable: true),
                    To = table.Column<long>(type: "bigint", nullable: true),
                    Gateway = table.Column<long>(type: "bigint", nullable: true),
                    FromOrTo = table.Column<long>(type: "bigint", nullable: true),
                    MqttServer = table.Column<string>(type: "text", nullable: true),
                    Channel = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Webhooks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Packets_MqttServerId",
                table: "Packets",
                column: "MqttServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_MqttServerId",
                table: "Nodes",
                column: "MqttServerId");

            migrationBuilder.CreateIndex(
                name: "IX_MqttServers_Name",
                table: "MqttServers",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_Nodes_MqttServers_MqttServerId",
                table: "Nodes",
                column: "MqttServerId",
                principalTable: "MqttServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_MqttServers_MqttServerId",
                table: "Packets",
                column: "MqttServerId",
                principalTable: "MqttServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Nodes_MqttServers_MqttServerId",
                table: "Nodes");

            migrationBuilder.DropForeignKey(
                name: "FK_Packets_MqttServers_MqttServerId",
                table: "Packets");

            migrationBuilder.DropTable(
                name: "MqttServers");

            migrationBuilder.DropTable(
                name: "Webhooks");

            migrationBuilder.DropIndex(
                name: "IX_Packets_MqttServerId",
                table: "Packets");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_MqttServerId",
                table: "Nodes");

            migrationBuilder.DropColumn(
                name: "MqttServerId",
                table: "Packets");

            migrationBuilder.DropColumn(
                name: "MqttServerId",
                table: "Nodes");
        }
    }
}
