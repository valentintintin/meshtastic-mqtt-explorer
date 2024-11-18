using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTraceroute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Traceroutes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Traceroutes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NodeFromId = table.Column<long>(type: "bigint", nullable: false),
                    NodeId = table.Column<long>(type: "bigint", nullable: false),
                    NodeToId = table.Column<long>(type: "bigint", nullable: false),
                    PacketId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Hop = table.Column<int>(type: "integer", nullable: false),
                    Snr = table.Column<double>(type: "double precision", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Traceroutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Traceroutes_Nodes_NodeFromId",
                        column: x => x.NodeFromId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Traceroutes_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Traceroutes_Nodes_NodeToId",
                        column: x => x.NodeToId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Traceroutes_Packets_PacketId",
                        column: x => x.PacketId,
                        principalTable: "Packets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_CreatedAt",
                table: "Traceroutes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_NodeFromId",
                table: "Traceroutes",
                column: "NodeFromId");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_NodeId",
                table: "Traceroutes",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_NodeToId",
                table: "Traceroutes",
                column: "NodeToId");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_PacketId",
                table: "Traceroutes",
                column: "PacketId");

            migrationBuilder.CreateIndex(
                name: "IX_Traceroutes_UpdatedAt",
                table: "Traceroutes",
                column: "UpdatedAt");
        }
    }
}
