using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class TextMessageAlwaysHaveTo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TextMessages_Nodes_ToId",
                table: "TextMessages");

            migrationBuilder.AlterColumn<long>(
                name: "ToId",
                table: "TextMessages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TextMessages_Nodes_ToId",
                table: "TextMessages",
                column: "ToId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TextMessages_Nodes_ToId",
                table: "TextMessages");

            migrationBuilder.AlterColumn<long>(
                name: "ToId",
                table: "TextMessages",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddForeignKey(
                name: "FK_TextMessages_Nodes_ToId",
                table: "TextMessages",
                column: "ToId",
                principalTable: "Nodes",
                principalColumn: "Id");
        }
    }
}
