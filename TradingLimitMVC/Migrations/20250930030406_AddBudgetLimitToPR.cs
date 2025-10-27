using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingLimitMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetLimitToPR : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BudgetLimit",
                table: "PurchaseRequisitions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 5000m);
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetLimit",
                table: "PurchaseRequisitions");
        }
    }
}
