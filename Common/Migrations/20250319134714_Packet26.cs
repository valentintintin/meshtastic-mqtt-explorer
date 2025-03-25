using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class Packet26 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "NextHop",
                table: "Packets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RelayNode",
                table: "Packets",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextHop",
                table: "Packets");

            migrationBuilder.DropColumn(
                name: "RelayNode",
                table: "Packets");
        }
    }
}
