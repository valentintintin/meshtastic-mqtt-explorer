using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class PacketDuplicated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "PacketId",
                table: "Packets",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)");

            migrationBuilder.AddColumn<long>(
                name: "PacketDuplicatedId",
                table: "Packets",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packets_PacketDuplicatedId",
                table: "Packets",
                column: "PacketDuplicatedId");

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Packets_PacketDuplicatedId",
                table: "Packets",
                column: "PacketDuplicatedId",
                principalTable: "Packets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Packets_PacketDuplicatedId",
                table: "Packets");

            migrationBuilder.DropIndex(
                name: "IX_Packets_PacketDuplicatedId",
                table: "Packets");

            migrationBuilder.DropColumn(
                name: "PacketDuplicatedId",
                table: "Packets");

            migrationBuilder.AlterColumn<decimal>(
                name: "PacketId",
                table: "Packets",
                type: "numeric(20,0)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
