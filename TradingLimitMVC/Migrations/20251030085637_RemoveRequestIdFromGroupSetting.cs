using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingLimitMVC.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRequestIdFromGroupSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalSteps_ApprovalWorkflows_ApprovalWorkflowId",
                table: "ApprovalSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalWorkflows_Temp_TL_TradingLimitRequests_TradingLimitRequestId",
                table: "ApprovalWorkflows");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApprovalWorkflows",
                table: "ApprovalWorkflows");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApprovalSteps",
                table: "ApprovalSteps");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApprovalNotifications",
                table: "ApprovalNotifications");

            migrationBuilder.RenameTable(
                name: "ApprovalWorkflows",
                newName: "Temp_TL_ApprovalWorkflows");

            migrationBuilder.RenameTable(
                name: "ApprovalSteps",
                newName: "Temp_TL_ApprovalSteps");

            migrationBuilder.RenameTable(
                name: "ApprovalNotifications",
                newName: "Temp_TL_ApprovalNotifications");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalWorkflows_WorkflowType",
                table: "Temp_TL_ApprovalWorkflows",
                newName: "IX_Temp_TL_ApprovalWorkflows_WorkflowType");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalWorkflows_TradingLimitRequestId",
                table: "Temp_TL_ApprovalWorkflows",
                newName: "IX_Temp_TL_ApprovalWorkflows_TradingLimitRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalWorkflows_Status",
                table: "Temp_TL_ApprovalWorkflows",
                newName: "IX_Temp_TL_ApprovalWorkflows_Status");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalSteps_StepNumber",
                table: "Temp_TL_ApprovalSteps",
                newName: "IX_Temp_TL_ApprovalSteps_StepNumber");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalSteps_Status",
                table: "Temp_TL_ApprovalSteps",
                newName: "IX_Temp_TL_ApprovalSteps_Status");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalSteps_ApproverEmail",
                table: "Temp_TL_ApprovalSteps",
                newName: "IX_Temp_TL_ApprovalSteps_ApproverEmail");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalSteps_ApprovalWorkflowId_StepNumber",
                table: "Temp_TL_ApprovalSteps",
                newName: "IX_Temp_TL_ApprovalSteps_ApprovalWorkflowId_StepNumber");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalSteps_ApprovalWorkflowId",
                table: "Temp_TL_ApprovalSteps",
                newName: "IX_Temp_TL_ApprovalSteps_ApprovalWorkflowId");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalNotifications_Type",
                table: "Temp_TL_ApprovalNotifications",
                newName: "IX_Temp_TL_ApprovalNotifications_Type");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalNotifications_RequestId_RequestType",
                table: "Temp_TL_ApprovalNotifications",
                newName: "IX_Temp_TL_ApprovalNotifications_RequestId_RequestType");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalNotifications_RequestId",
                table: "Temp_TL_ApprovalNotifications",
                newName: "IX_Temp_TL_ApprovalNotifications_RequestId");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalNotifications_RecipientEmail",
                table: "Temp_TL_ApprovalNotifications",
                newName: "IX_Temp_TL_ApprovalNotifications_RecipientEmail");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Temp_TL_ApprovalWorkflows",
                table: "Temp_TL_ApprovalWorkflows",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Temp_TL_ApprovalSteps",
                table: "Temp_TL_ApprovalSteps",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Temp_TL_ApprovalNotifications",
                table: "Temp_TL_ApprovalNotifications",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "GroupSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupID = table.Column<int>(type: "int", nullable: false),
                    TypeID = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupSettings_Email",
                table: "GroupSettings",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_GroupSettings_GroupID",
                table: "GroupSettings",
                column: "GroupID");

            migrationBuilder.CreateIndex(
                name: "IX_GroupSettings_TypeID",
                table: "GroupSettings",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_GroupSettings_Unique_Group_Type_Email",
                table: "GroupSettings",
                columns: new[] { "GroupID", "TypeID", "Email" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Temp_TL_ApprovalSteps_Temp_TL_ApprovalWorkflows_ApprovalWorkflowId",
                table: "Temp_TL_ApprovalSteps",
                column: "ApprovalWorkflowId",
                principalTable: "Temp_TL_ApprovalWorkflows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Temp_TL_ApprovalWorkflows_Temp_TL_TradingLimitRequests_TradingLimitRequestId",
                table: "Temp_TL_ApprovalWorkflows",
                column: "TradingLimitRequestId",
                principalTable: "Temp_TL_TradingLimitRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Temp_TL_ApprovalSteps_Temp_TL_ApprovalWorkflows_ApprovalWorkflowId",
                table: "Temp_TL_ApprovalSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_Temp_TL_ApprovalWorkflows_Temp_TL_TradingLimitRequests_TradingLimitRequestId",
                table: "Temp_TL_ApprovalWorkflows");

            migrationBuilder.DropTable(
                name: "GroupSettings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Temp_TL_ApprovalWorkflows",
                table: "Temp_TL_ApprovalWorkflows");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Temp_TL_ApprovalSteps",
                table: "Temp_TL_ApprovalSteps");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Temp_TL_ApprovalNotifications",
                table: "Temp_TL_ApprovalNotifications");

            migrationBuilder.RenameTable(
                name: "Temp_TL_ApprovalWorkflows",
                newName: "ApprovalWorkflows");

            migrationBuilder.RenameTable(
                name: "Temp_TL_ApprovalSteps",
                newName: "ApprovalSteps");

            migrationBuilder.RenameTable(
                name: "Temp_TL_ApprovalNotifications",
                newName: "ApprovalNotifications");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_ApprovalWorkflows_WorkflowType",
                table: "ApprovalWorkflows",
                newName: "IX_ApprovalWorkflows_WorkflowType");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_ApprovalWorkflows_TradingLimitRequestId",
                table: "ApprovalWorkflows",
                newName: "IX_ApprovalWorkflows_TradingLimitRequestId");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_ApprovalWorkflows_Status",
                table: "ApprovalWorkflows",
                newName: "IX_ApprovalWorkflows_Status");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_ApprovalSteps_StepNumber",
                table: "ApprovalSteps",
                newName: "IX_ApprovalSteps_StepNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_ApprovalSteps_Status",
                table: "ApprovalSteps",
                newName: "IX_ApprovalSteps_Status");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_ApprovalSteps_ApproverEmail",
                table: "ApprovalSteps",
                newName: "IX_ApprovalSteps_ApproverEmail");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_ApprovalSteps_ApprovalWorkflowId_StepNumber",
                table: "ApprovalSteps",
                newName: "IX_ApprovalSteps_ApprovalWorkflowId_StepNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_ApprovalSteps_ApprovalWorkflowId",
                table: "ApprovalSteps",
                newName: "IX_ApprovalSteps_ApprovalWorkflowId");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_ApprovalNotifications_Type",
                table: "ApprovalNotifications",
                newName: "IX_ApprovalNotifications_Type");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_ApprovalNotifications_RequestId_RequestType",
                table: "ApprovalNotifications",
                newName: "IX_ApprovalNotifications_RequestId_RequestType");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_ApprovalNotifications_RequestId",
                table: "ApprovalNotifications",
                newName: "IX_ApprovalNotifications_RequestId");

            migrationBuilder.RenameIndex(
                name: "IX_Temp_TL_ApprovalNotifications_RecipientEmail",
                table: "ApprovalNotifications",
                newName: "IX_ApprovalNotifications_RecipientEmail");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApprovalWorkflows",
                table: "ApprovalWorkflows",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApprovalSteps",
                table: "ApprovalSteps",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApprovalNotifications",
                table: "ApprovalNotifications",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalSteps_ApprovalWorkflows_ApprovalWorkflowId",
                table: "ApprovalSteps",
                column: "ApprovalWorkflowId",
                principalTable: "ApprovalWorkflows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalWorkflows_Temp_TL_TradingLimitRequests_TradingLimitRequestId",
                table: "ApprovalWorkflows",
                column: "TradingLimitRequestId",
                principalTable: "Temp_TL_TradingLimitRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
