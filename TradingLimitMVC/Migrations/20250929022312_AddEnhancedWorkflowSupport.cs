using System;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace TradingLimitMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddEnhancedWorkflowSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns to PurchaseRequisitions
            migrationBuilder.AddColumn<int>(
                name: "DistributionType",
                table: "PurchaseRequisitions",
                type: "int",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.AddColumn<decimal>(
                name: "DistributionTotal",
                table: "PurchaseRequisitions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
            migrationBuilder.AddColumn<string>(
                name: "DistributionCurrency",
                table: "PurchaseRequisitions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "CurrentApprovalStep",
                table: "PurchaseRequisitions",
                type: "int",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.AddColumn<int>(
                name: "TotalApprovalSteps",
                table: "PurchaseRequisitions",
                type: "int",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "PurchaseRequisitions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "RejectedBy",
                table: "PurchaseRequisitions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedDate",
                table: "PurchaseRequisitions",
                type: "datetime2",
                nullable: true);
            // Add new fields to PurchaseRequisitionApprovals
            migrationBuilder.AddColumn<string>(
                name: "ApproverRole",
                table: "PurchaseRequisitionApprovals",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "ApprovalMethod",
                table: "PurchaseRequisitionApprovals",
                type: "int",
                nullable: false,
                defaultValue: 0);
            // Create ApprovalWorkflowSteps table
            migrationBuilder.CreateTable(
                name: "ApprovalWorkflowStep",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseRequisitionId = table.Column<int>(type: "int", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    ApproverRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApproverName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApproverEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsParallel = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ApprovalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalWorkflowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalWorkflowSteps_PurchaseRequisitions_PurchaseRequisitionId",
                        column: x => x.PurchaseRequisitionId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowSteps_PurchaseRequisitionId",
                table: "ApprovalWorkflowStep",
                column: "PurchaseRequisitionId");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ApprovalWorkflowStep");
            migrationBuilder.DropColumn(name: "DistributionType", table: "PurchaseRequisitions");
            migrationBuilder.DropColumn(name: "DistributionTotal", table: "PurchaseRequisitions");
            migrationBuilder.DropColumn(name: "DistributionCurrency", table: "PurchaseRequisitions");
            migrationBuilder.DropColumn(name: "CurrentApprovalStep", table: "PurchaseRequisitions");
            migrationBuilder.DropColumn(name: "TotalApprovalSteps", table: "PurchaseRequisitions");
            migrationBuilder.DropColumn(name: "RejectionReason", table: "PurchaseRequisitions");
            migrationBuilder.DropColumn(name: "RejectedBy", table: "PurchaseRequisitions");
            migrationBuilder.DropColumn(name: "RejectedDate", table: "PurchaseRequisitions");
            migrationBuilder.DropColumn(name: "ApproverRole", table: "PurchaseRequisitionApprovals");
            migrationBuilder.DropColumn(name: "ApprovalMethod", table: "PurchaseRequisitionApprovals");
        }
    }
}
