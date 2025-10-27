using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingLimitMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddCostCenterSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "CostCenters",
                type: "int",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "CostCenters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedDate",
                table: "CostCenters",
                type: "datetime2",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "ApprovalComments",
                table: "CostCenters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "ApproverEmail",
                table: "CostCenters",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ApprovalStatus", table: "CostCenters");
            migrationBuilder.DropColumn(name: "ApprovedBy", table: "CostCenters");
            migrationBuilder.DropColumn(name: "ApprovedDate", table: "CostCenters");
            migrationBuilder.DropColumn(name: "ApprovalComments", table: "CostCenters");
            migrationBuilder.DropColumn(name: "ApproverEmail", table: "CostCenters");
        }
    }
}
