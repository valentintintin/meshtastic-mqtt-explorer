using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class WebhookPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DistanceAroundPositionKm",
                table: "Webhooks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Webhooks",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Webhooks",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DistanceAroundPositionKm",
                table: "Webhooks");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Webhooks");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Webhooks");
        }
    }
}
