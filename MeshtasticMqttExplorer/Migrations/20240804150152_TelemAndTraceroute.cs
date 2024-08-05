using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class TelemAndTraceroute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "Channel1Current",
                table: "Telemetries",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Channel1Voltage",
                table: "Telemetries",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Channel2Current",
                table: "Telemetries",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Channel2Voltage",
                table: "Telemetries",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Channel3Current",
                table: "Telemetries",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Channel3Voltage",
                table: "Telemetries",
                type: "real",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "HopStart",
                table: "Packets",
                type: "integer",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "HopLimit",
                table: "Packets",
                type: "integer",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Traceroutes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeId = table.Column<long>(type: "bigint", nullable: false),
                    Hop = table.Column<int>(type: "integer", nullable: false),
                    PacketId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Traceroutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Traceroutes_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Traceroutes_Packets_PacketId",
                        column: x => x.PacketId,
                        principalTable: "Packets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_NodeId",
                table: "Traceroutes",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_PacketId",
                table: "Traceroutes",
                column: "PacketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Traceroutes");

            migrationBuilder.DropColumn(
                name: "Channel1Current",
                table: "Telemetries");

            migrationBuilder.DropColumn(
                name: "Channel1Voltage",
                table: "Telemetries");

            migrationBuilder.DropColumn(
                name: "Channel2Current",
                table: "Telemetries");

            migrationBuilder.DropColumn(
                name: "Channel2Voltage",
                table: "Telemetries");

            migrationBuilder.DropColumn(
                name: "Channel3Current",
                table: "Telemetries");

            migrationBuilder.DropColumn(
                name: "Channel3Voltage",
                table: "Telemetries");

            migrationBuilder.AlterColumn<long>(
                name: "HopStart",
                table: "Packets",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "HopLimit",
                table: "Packets",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
