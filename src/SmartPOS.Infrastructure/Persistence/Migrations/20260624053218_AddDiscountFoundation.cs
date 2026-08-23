using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscountApprovedByUserId",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountReason",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "SaleLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountReason",
                table: "SaleLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountReason",
                table: "HeldSales",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "HeldSaleLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountReason",
                table: "HeldSaleLines",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountApprovedByUserId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DiscountReason",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "DiscountReason",
                table: "SaleLines");

            migrationBuilder.DropColumn(
                name: "DiscountReason",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "HeldSaleLines");

            migrationBuilder.DropColumn(
                name: "DiscountReason",
                table: "HeldSaleLines");
        }
    }
}
