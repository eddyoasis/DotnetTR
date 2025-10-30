using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingLimitMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalGroupToApprovalSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GroupSettings",
                table: "GroupSettings");

            migrationBuilder.RenameTable(
                name: "GroupSettings",
                newName: "Temp_TL_GroupSettings");

            migrationBuilder.AddColumn<int>(
                name: "ApprovalGroupId",
                table: "Temp_TL_ApprovalSteps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalGroupName",
                table: "Temp_TL_ApprovalSteps",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Temp_TL_GroupSettings",
                table: "Temp_TL_GroupSettings",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Temp_TL_GroupSettings",
                table: "Temp_TL_GroupSettings");

            migrationBuilder.DropColumn(
                name: "ApprovalGroupId",
                table: "Temp_TL_ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "ApprovalGroupName",
                table: "Temp_TL_ApprovalSteps");

            migrationBuilder.RenameTable(
                name: "Temp_TL_GroupSettings",
                newName: "GroupSettings");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GroupSettings",
                table: "GroupSettings",
                column: "Id");
        }
    }
}
