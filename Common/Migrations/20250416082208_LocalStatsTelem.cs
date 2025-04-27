using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class LocalStatsTelem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "NumPacketsRx",
                table: "Telemetries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NumPacketsRxBad",
                table: "Telemetries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NumPacketsTx",
                table: "Telemetries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NumRxDupe",
                table: "Telemetries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NumTxRelay",
                table: "Telemetries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NumTxRelayCanceled",
                table: "Telemetries",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumPacketsRx",
                table: "Telemetries");

            migrationBuilder.DropColumn(
                name: "NumPacketsRxBad",
                table: "Telemetries");

            migrationBuilder.DropColumn(
                name: "NumPacketsTx",
                table: "Telemetries");

            migrationBuilder.DropColumn(
                name: "NumRxDupe",
                table: "Telemetries");

            migrationBuilder.DropColumn(
                name: "NumTxRelay",
                table: "Telemetries");

            migrationBuilder.DropColumn(
                name: "NumTxRelayCanceled",
                table: "Telemetries");
        }
    }
}
