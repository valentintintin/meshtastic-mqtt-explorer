using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class TelemetryIaqGaz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "GasResistance",
                table: "Telemetries",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Iaq",
                table: "Telemetries",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GasResistance",
                table: "Telemetries");

            migrationBuilder.DropColumn(
                name: "Iaq",
                table: "Telemetries");
        }
    }
}
