using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class PaxCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaxCounters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeId = table.Column<long>(type: "bigint", nullable: false),
                    PacketId = table.Column<long>(type: "bigint", nullable: true),
                    Wifi = table.Column<long>(type: "bigint", nullable: false),
                    Ble = table.Column<long>(type: "bigint", nullable: false),
                    Uptime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaxCounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaxCounters_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaxCounters_Packets_PacketId",
                        column: x => x.PacketId,
                        principalTable: "Packets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaxCounters_CreatedAt",
                table: "PaxCounters",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaxCounters_NodeId",
                table: "PaxCounters",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaxCounters_PacketId",
                table: "PaxCounters",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_PaxCounters_UpdatedAt",
                table: "PaxCounters",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaxCounters");
        }
    }
}
