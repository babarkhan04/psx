using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHeldSaleExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiredOn",
                table: "HeldSales",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresOn",
                table: "HeldSales",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpiryReason",
                table: "HeldSales",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HeldSales_ExpiresOn",
                table: "HeldSales",
                column: "ExpiresOn");

            migrationBuilder.CreateIndex(
                name: "IX_HeldSales_StoreId_RegisterId_Status_ExpiresOn",
                table: "HeldSales",
                columns: new[] { "StoreId", "RegisterId", "Status", "ExpiresOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HeldSales_ExpiresOn",
                table: "HeldSales");

            migrationBuilder.DropIndex(
                name: "IX_HeldSales_StoreId_RegisterId_Status_ExpiresOn",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "ExpiredOn",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "ExpiresOn",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "ExpiryReason",
                table: "HeldSales");
        }
    }
}
