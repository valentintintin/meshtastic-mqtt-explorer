using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class Seventh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.AlterColumn<long>(
            //     name: "GatewayId",
            //     table: "Packets",
            //     type: "bigint",
            //     nullable: false,
            //     oldClrType: typeof(string),
            //     oldType: "character varying(16)",
            //     oldMaxLength: 16);
            migrationBuilder.Sql("ALTER TABLE \"Packets\" ALTER COLUMN \"GatewayId\" TYPE bigint USING (\"GatewayId\"::bigint);");

            migrationBuilder.CreateIndex(
                name: "IX_Packets_GatewayId",
                table: "Packets",
                column: "GatewayId");

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Nodes_GatewayId",
                table: "Packets",
                column: "GatewayId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Nodes_GatewayId",
                table: "Packets");

            migrationBuilder.DropIndex(
                name: "IX_Packets_GatewayId",
                table: "Packets");

            migrationBuilder.AlterColumn<string>(
                name: "GatewayId",
                table: "Packets",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
