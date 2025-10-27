using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingLimitMVC.Migrations
{
    /// <inheritdoc />
    public partial class FixMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetLimit",
                table: "PurchaseRequisitions");

            migrationBuilder.AddColumn<int>(
                name: "CurrentApprovalStep",
                table: "PurchaseOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentStatus",
                table: "PurchaseOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalApprovalDate",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalApprover",
                table: "PurchaseOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedBy",
                table: "PurchaseOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedDate",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalApprovalSteps",
                table: "PurchaseOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "POApprovalWorkflowSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    ApproverRole = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApproverName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApproverEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POApprovalWorkflowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_POApprovalWorkflowSteps_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: false),
                    ApproverName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApproverEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovalLevel = table.Column<int>(type: "int", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovalMethod = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderApprovals_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_POApprovalWorkflowSteps_PurchaseOrderId",
                table: "POApprovalWorkflowSteps",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderApprovals_PurchaseOrderId",
                table: "PurchaseOrderApprovals",
                column: "PurchaseOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "POApprovalWorkflowSteps");

            migrationBuilder.DropTable(
                name: "PurchaseOrderApprovals");

            migrationBuilder.DropColumn(
                name: "CurrentApprovalStep",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CurrentStatus",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "FinalApprovalDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "FinalApprover",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SubmittedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SubmittedDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TotalApprovalSteps",
                table: "PurchaseOrders");

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetLimit",
                table: "PurchaseRequisitions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
