using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceReturnsExchange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExchangeReference",
                table: "SaleReturns",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundReference",
                table: "SaleReturns",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnReason",
                table: "SaleReturns",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Disposition",
                table: "SaleReturnLines",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LineReason",
                table: "SaleReturnLines",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeReference",
                table: "SaleReturns");

            migrationBuilder.DropColumn(
                name: "RefundReference",
                table: "SaleReturns");

            migrationBuilder.DropColumn(
                name: "ReturnReason",
                table: "SaleReturns");

            migrationBuilder.DropColumn(
                name: "Disposition",
                table: "SaleReturnLines");

            migrationBuilder.DropColumn(
                name: "LineReason",
                table: "SaleReturnLines");
        }
    }
}
