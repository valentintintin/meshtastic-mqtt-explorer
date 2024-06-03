using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeshtasticMqttExplorer.Migrations
{
    /// <inheritdoc />
    public partial class Fourth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TextMessages_Nodes_NodeId",
                table: "TextMessages");

            migrationBuilder.RenameColumn(
                name: "NodeId",
                table: "TextMessages",
                newName: "FromId");

            migrationBuilder.RenameIndex(
                name: "IX_TextMessages_NodeId",
                table: "TextMessages",
                newName: "IX_TextMessages_FromId");

            migrationBuilder.AddColumn<long>(
                name: "ToId",
                table: "TextMessages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MqttServer",
                table: "Packets",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TextMessages_ToId",
                table: "TextMessages",
                column: "ToId");

            migrationBuilder.AddForeignKey(
                name: "FK_TextMessages_Nodes_FromId",
                table: "TextMessages",
                column: "FromId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TextMessages_Nodes_ToId",
                table: "TextMessages",
                column: "ToId",
                principalTable: "Nodes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TextMessages_Nodes_FromId",
                table: "TextMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_TextMessages_Nodes_ToId",
                table: "TextMessages");

            migrationBuilder.DropIndex(
                name: "IX_TextMessages_ToId",
                table: "TextMessages");

            migrationBuilder.DropColumn(
                name: "ToId",
                table: "TextMessages");

            migrationBuilder.DropColumn(
                name: "MqttServer",
                table: "Packets");

            migrationBuilder.RenameColumn(
                name: "FromId",
                table: "TextMessages",
                newName: "NodeId");

            migrationBuilder.RenameIndex(
                name: "IX_TextMessages_FromId",
                table: "TextMessages",
                newName: "IX_TextMessages_NodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_TextMessages_Nodes_NodeId",
                table: "TextMessages",
                column: "NodeId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
