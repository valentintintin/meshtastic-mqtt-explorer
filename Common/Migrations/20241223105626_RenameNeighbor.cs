using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class RenameNeighbor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Nodes_NeighborId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Nodes_NodeId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Positions_NeighborPositionId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Positions_NodePositionId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_SignalHistories_Nodes_FromId",
                table: "SignalHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_SignalHistories_Nodes_ToId",
                table: "SignalHistories");

            migrationBuilder.RenameColumn(
                name: "ToId",
                table: "SignalHistories",
                newName: "NodeReceiverId");

            migrationBuilder.RenameColumn(
                name: "FromId",
                table: "SignalHistories",
                newName: "NodeHeardId");

            migrationBuilder.RenameIndex(
                name: "IX_SignalHistories_ToId",
                table: "SignalHistories",
                newName: "IX_SignalHistories_NodeReceiverId");

            migrationBuilder.RenameIndex(
                name: "IX_SignalHistories_FromId",
                table: "SignalHistories",
                newName: "IX_SignalHistories_NodeHeardId");

            migrationBuilder.RenameColumn(
                name: "NodePositionId",
                table: "NeighborInfos",
                newName: "NodeReceiverPositionId");

            migrationBuilder.RenameColumn(
                name: "NodeId",
                table: "NeighborInfos",
                newName: "NodeReceiverId");

            migrationBuilder.RenameColumn(
                name: "NeighborPositionId",
                table: "NeighborInfos",
                newName: "NodeHeardPositionId");

            migrationBuilder.RenameColumn(
                name: "NeighborId",
                table: "NeighborInfos",
                newName: "NodeHeardId");

            migrationBuilder.RenameIndex(
                name: "IX_NeighborInfos_NodePositionId",
                table: "NeighborInfos",
                newName: "IX_NeighborInfos_NodeReceiverPositionId");

            migrationBuilder.RenameIndex(
                name: "IX_NeighborInfos_NodeId",
                table: "NeighborInfos",
                newName: "IX_NeighborInfos_NodeReceiverId");

            migrationBuilder.RenameIndex(
                name: "IX_NeighborInfos_NeighborPositionId",
                table: "NeighborInfos",
                newName: "IX_NeighborInfos_NodeHeardPositionId");

            migrationBuilder.RenameIndex(
                name: "IX_NeighborInfos_NeighborId",
                table: "NeighborInfos",
                newName: "IX_NeighborInfos_NodeHeardId");

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Nodes_NodeHeardId",
                table: "NeighborInfos",
                column: "NodeHeardId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Nodes_NodeReceiverId",
                table: "NeighborInfos",
                column: "NodeReceiverId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Positions_NodeHeardPositionId",
                table: "NeighborInfos",
                column: "NodeHeardPositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Positions_NodeReceiverPositionId",
                table: "NeighborInfos",
                column: "NodeReceiverPositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SignalHistories_Nodes_NodeHeardId",
                table: "SignalHistories",
                column: "NodeHeardId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SignalHistories_Nodes_NodeReceiverId",
                table: "SignalHistories",
                column: "NodeReceiverId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Nodes_NodeHeardId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Nodes_NodeReceiverId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Positions_NodeHeardPositionId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_NeighborInfos_Positions_NodeReceiverPositionId",
                table: "NeighborInfos");

            migrationBuilder.DropForeignKey(
                name: "FK_SignalHistories_Nodes_NodeHeardId",
                table: "SignalHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_SignalHistories_Nodes_NodeReceiverId",
                table: "SignalHistories");

            migrationBuilder.RenameColumn(
                name: "NodeReceiverId",
                table: "SignalHistories",
                newName: "ToId");

            migrationBuilder.RenameColumn(
                name: "NodeHeardId",
                table: "SignalHistories",
                newName: "FromId");

            migrationBuilder.RenameIndex(
                name: "IX_SignalHistories_NodeReceiverId",
                table: "SignalHistories",
                newName: "IX_SignalHistories_ToId");

            migrationBuilder.RenameIndex(
                name: "IX_SignalHistories_NodeHeardId",
                table: "SignalHistories",
                newName: "IX_SignalHistories_FromId");

            migrationBuilder.RenameColumn(
                name: "NodeReceiverPositionId",
                table: "NeighborInfos",
                newName: "NodePositionId");

            migrationBuilder.RenameColumn(
                name: "NodeReceiverId",
                table: "NeighborInfos",
                newName: "NodeId");

            migrationBuilder.RenameColumn(
                name: "NodeHeardPositionId",
                table: "NeighborInfos",
                newName: "NeighborPositionId");

            migrationBuilder.RenameColumn(
                name: "NodeHeardId",
                table: "NeighborInfos",
                newName: "NeighborId");

            migrationBuilder.RenameIndex(
                name: "IX_NeighborInfos_NodeReceiverPositionId",
                table: "NeighborInfos",
                newName: "IX_NeighborInfos_NodePositionId");

            migrationBuilder.RenameIndex(
                name: "IX_NeighborInfos_NodeReceiverId",
                table: "NeighborInfos",
                newName: "IX_NeighborInfos_NodeId");

            migrationBuilder.RenameIndex(
                name: "IX_NeighborInfos_NodeHeardPositionId",
                table: "NeighborInfos",
                newName: "IX_NeighborInfos_NeighborPositionId");

            migrationBuilder.RenameIndex(
                name: "IX_NeighborInfos_NodeHeardId",
                table: "NeighborInfos",
                newName: "IX_NeighborInfos_NeighborId");

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Nodes_NeighborId",
                table: "NeighborInfos",
                column: "NeighborId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Nodes_NodeId",
                table: "NeighborInfos",
                column: "NodeId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Positions_NeighborPositionId",
                table: "NeighborInfos",
                column: "NeighborPositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NeighborInfos_Positions_NodePositionId",
                table: "NeighborInfos",
                column: "NodePositionId",
                principalTable: "Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SignalHistories_Nodes_FromId",
                table: "SignalHistories",
                column: "FromId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SignalHistories_Nodes_ToId",
                table: "SignalHistories",
                column: "ToId",
                principalTable: "Nodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
