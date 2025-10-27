using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingLimitMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddCostCenterApprovalTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "IsRequired",
                table: "CostCenters",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ApproverRole", table: "CostCenters");
            migrationBuilder.DropColumn(name: "ApprovalOrder", table: "CostCenters");
            migrationBuilder.DropColumn(name: "IsRequired", table: "CostCenters");
        }
    }
}
