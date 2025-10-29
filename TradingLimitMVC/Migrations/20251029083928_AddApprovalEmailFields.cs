using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingLimitMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalEmailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TradingLimitRequestAttachments_TradingLimitRequests_TradingLimitRequestId",
                table: "TradingLimitRequestAttachments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TradingLimitRequests",
                table: "TradingLimitRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TradingLimitRequestAttachments",
                table: "TradingLimitRequestAttachments");

            migrationBuilder.RenameTable(
                name: "TradingLimitRequests",
                newName: "Temp_TL_TradingLimitRequests");

            migrationBuilder.RenameTable(
                name: "TradingLimitRequestAttachments",
                newName: "Temp_TL_TradingLimitRequestAttachments");

            migrationBuilder.RenameIndex(
                name: "IX_TradingLimitRequests_TRCode",
                table: "Temp_TL_TradingLimitRequests",
                newName: "IX_Temp_TL_TradingLimitRequests_TRCode");

            migrationBuilder.RenameIndex(
                name: "IX_TradingLimitRequests_Status",
                table: "Temp_TL_TradingLimitRequests",
                newName: "IX_Temp_TL_TradingLimitRequests_Status");

            migrationBuilder.RenameIndex(
                name: "IX_TradingLimitRequests_RequestId",
                table: "Temp_TL_TradingLimitRequests",
                newName: "IX_Temp_TL_TradingLimitRequests_RequestId");

            migrationBuilder.RenameIndex(
                name: "IX_TradingLimitRequests_ClientCode",
                table: "Temp_TL_TradingLimitRequests",
                newName: "IX_Temp_TL_TradingLimitRequests_ClientCode");

            migrationBuilder.RenameIndex(
                name: "IX_TradingLimitRequestAttachments_TradingLimitRequestId",
                table: "Temp_TL_TradingLimitRequestAttachments",
                newName: "IX_Temp_TL_TradingLimitRequestAttachments_TradingLimitRequestId");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalComments",
                table: "Temp_TL_TradingLimitRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalEmail",
                table: "Temp_TL_TradingLimitRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "Temp_TL_TradingLimitRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedDate",
                table: "Temp_TL_TradingLimitRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Temp_TL_TradingLimitRequests",
                table: "Temp_TL_TradingLimitRequests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Temp_TL_TradingLimitRequestAttachments",
                table: "Temp_TL_TradingLimitRequestAttachments",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Temp_TL_TradingLimitRequests_ApprovalEmail",
                table: "Temp_TL_TradingLimitRequests",
                column: "ApprovalEmail");

            migrationBuilder.AddForeignKey(
                name: "FK_Temp_TL_TradingLimitRequestAttachments_Temp_TL_TradingLimitRequests_TradingLimitRequestId",
                table: "Temp_TL_TradingLimitRequestAttachments",
                column: "TradingLimitRequestId",
                principalTable: "Temp_TL_TradingLimitRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Temp_TL_TradingLimitRequestAttachments_Temp_TL_TradingLimitRequests_TradingLimitRequestId",
                table: "Temp_TL_TradingLimitRequestAttachments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Temp_TL_TradingLimitRequests",
                table: "Temp_TL_TradingLimitRequests");

            migrationBuilder.DropIndex(
                name: "IX_Temp_TL_TradingLimitRequests_ApprovalEmail",
                table: "Temp_TL_TradingLimitRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Temp_TL_TradingLimitRequestAttachments",
                table: "Temp_TL_TradingLimitRequestAttachments");

            migrationBuilder.DropColumn(
                name: "ApprovalComments",
                table: "Temp_TL_TradingLimitRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalEmail",
                table: "Temp_TL_TradingLimitRequests");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "Temp_TL_TradingLimitRequests");

            migrationBuilder.DropColumn(
                name: "ApprovedDate",
                table: "Temp_TL_TradingLimitRequests");

            migrationBuilder.RenameTable(
                name: "Temp_TL_TradingLimitRequests",
                newName: "TradingLimitRequests");

            migrationBuilder.RenameTable(
                name: "Temp_TL_TradingLimitRequestAttachments",
                newName: "TradingLimitRequestAttachments");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_TradingLimitRequests_TRCode",
                table: "TradingLimitRequests",
                newName: "IX_TradingLimitRequests_TRCode");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_TradingLimitRequests_Status",
                table: "TradingLimitRequests",
                newName: "IX_TradingLimitRequests_Status");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_TradingLimitRequests_RequestId",
                table: "TradingLimitRequests",
                newName: "IX_TradingLimitRequests_RequestId");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_TradingLimitRequests_ClientCode",
                table: "TradingLimitRequests",
                newName: "IX_TradingLimitRequests_ClientCode");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_TradingLimitRequestAttachments_TradingLimitRequestId",
                table: "TradingLimitRequestAttachments",
                newName: "IX_TradingLimitRequestAttachments_TradingLimitRequestId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TradingLimitRequests",
                table: "TradingLimitRequests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TradingLimitRequestAttachments",
                table: "TradingLimitRequestAttachments",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TradingLimitRequestAttachments_TradingLimitRequests_TradingLimitRequestId",
                table: "TradingLimitRequestAttachments",
                column: "TradingLimitRequestId",
                principalTable: "TradingLimitRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
