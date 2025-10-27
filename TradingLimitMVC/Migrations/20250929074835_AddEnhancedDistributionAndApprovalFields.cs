using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingLimitMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddEnhancedDistributionAndApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpenseCode",
                table: "PurchaseRequisitions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectCode",
                table: "PurchaseRequisitions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorFullAddress",
                table: "PurchaseRequisitions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDistributionValid",
                table: "PurchaseRequisitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ApproverRole",
                table: "CostCenters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalOrder",
                table: "CostCenters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "CanModifyDistribution",
                table: "CostCenters",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DistributionType",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ExpenseCode",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "IsDistributionValid",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ProjectCode",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "VendorFullAddress",
                table: "PurchaseRequisitions");

            migrationBuilder.AlterColumn<string>(
                name: "Comments",
                table: "ApprovalWorkflowStep",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalWorkflowStepId",
                table: "ApprovalWorkflowStep",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowStep_ApprovalWorkflowStepId",
                table: "ApprovalWorkflowStep",
                column: "ApprovalWorkflowStepId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalWorkflowStep_ApprovalWorkflowStep_ApprovalWorkflowStepId",
                table: "ApprovalWorkflowStep",
                column: "ApprovalWorkflowStepId",
                principalTable: "ApprovalWorkflowStep",
                principalColumn: "Id");
        }
    }
}
