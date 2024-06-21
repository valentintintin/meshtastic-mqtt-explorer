using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class Waypoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Waypoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeId = table.Column<long>(type: "bigint", nullable: false),
                    PacketId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Icon = table.Column<long>(type: "bigint", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Waypoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Waypoints_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Waypoints_Packets_PacketId",
                        column: x => x.PacketId,
                        principalTable: "Packets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Telemetries_UpdatedAt",
                table: "Telemetries",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_UpdatedAt",
                table: "Positions",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_UpdatedAt",
                table: "Nodes",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_CreatedAt",
                table: "Waypoints",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_ExpiresAt",
                table: "Waypoints",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_Name",
                table: "Waypoints",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_NodeId",
                table: "Waypoints",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_PacketId",
                table: "Waypoints",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_Waypoints_UpdatedAt",
                table: "Waypoints",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Waypoints");

            migrationBuilder.DropIndex(
                name: "IX_Telemetries_UpdatedAt",
                table: "Telemetries");

            migrationBuilder.DropIndex(
                name: "IX_Positions_UpdatedAt",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_Nodes_UpdatedAt",
                table: "Nodes");
        }
    }
}
