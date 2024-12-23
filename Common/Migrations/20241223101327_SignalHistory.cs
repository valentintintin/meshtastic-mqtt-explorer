using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class SignalHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<float>(
                name: "Snr",
                table: "NeighborInfos",
                type: "real",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<float>(
                name: "Rssi",
                table: "NeighborInfos",
                type: "real",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SignalHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FromId = table.Column<long>(type: "bigint", nullable: false),
                    ToId = table.Column<long>(type: "bigint", nullable: false),
                    Snr = table.Column<float>(type: "real", nullable: false),
                    Rssi = table.Column<float>(type: "real", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignalHistories_Nodes_FromId",
                        column: x => x.FromId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignalHistories_Nodes_ToId",
                        column: x => x.ToId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignalHistories_CreatedAt",
                table: "SignalHistories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SignalHistories_FromId",
                table: "SignalHistories",
                column: "FromId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalHistories_ToId",
                table: "SignalHistories",
                column: "ToId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignalHistories");

            migrationBuilder.DropColumn(
                name: "Rssi",
                table: "NeighborInfos");

            migrationBuilder.AlterColumn<double>(
                name: "Snr",
                table: "NeighborInfos",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(float),
                oldType: "real");
        }
    }
}
