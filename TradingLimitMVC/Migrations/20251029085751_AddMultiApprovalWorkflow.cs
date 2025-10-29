using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingLimitMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalWorkflows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradingLimitRequestId = table.Column<int>(type: "int", nullable: false),
                    WorkflowType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrentStep = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequiredApprovals = table.Column<int>(type: "int", nullable: false),
                    ReceivedApprovals = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalWorkflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalWorkflows_Temp_TL_TradingLimitRequests_TradingLimitRequestId",
                        column: x => x.TradingLimitRequestId,
                        principalTable: "Temp_TL_TradingLimitRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApprovalWorkflowId = table.Column<int>(type: "int", nullable: false),
                    StepNumber = table.Column<int>(type: "int", nullable: false),
                    ApproverEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApproverName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApproverRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastReminderSent = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    MinimumAmountThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaximumAmountThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RequiredDepartment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovalConditions = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_ApprovalWorkflows_ApprovalWorkflowId",
                        column: x => x.ApprovalWorkflowId,
                        principalTable: "ApprovalWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_ApprovalWorkflowId",
                table: "ApprovalSteps",
                column: "ApprovalWorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_ApprovalWorkflowId_StepNumber",
                table: "ApprovalSteps",
                columns: new[] { "ApprovalWorkflowId", "StepNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_ApproverEmail",
                table: "ApprovalSteps",
                column: "ApproverEmail");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_Status",
                table: "ApprovalSteps",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_StepNumber",
                table: "ApprovalSteps",
                column: "StepNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_Status",
                table: "ApprovalWorkflows",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_TradingLimitRequestId",
                table: "ApprovalWorkflows",
                column: "TradingLimitRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflows_WorkflowType",
                table: "ApprovalWorkflows",
                column: "WorkflowType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalSteps");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflows");
        }
    }
}
