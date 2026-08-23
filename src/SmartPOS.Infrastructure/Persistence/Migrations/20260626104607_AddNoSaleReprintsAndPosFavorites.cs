using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNoSaleReprintsAndPosFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPosFavorite",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PosFavoriteSortOrder",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ReceiptReprintLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReprintedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReprintedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ReprintedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptReprintLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptReprintLogs_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_IsPosFavorite_PosFavoriteSortOrder",
                table: "Products",
                columns: new[] { "TenantId", "IsPosFavorite", "PosFavoriteSortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptReprintLogs_SaleId_ReprintedOn",
                table: "ReceiptReprintLogs",
                columns: new[] { "SaleId", "ReprintedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptReprintLogs_StoreId_ReprintedOn",
                table: "ReceiptReprintLogs",
                columns: new[] { "StoreId", "ReprintedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiptReprintLogs");

            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_IsPosFavorite_PosFavoriteSortOrder",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsPosFavorite",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PosFavoriteSortOrder",
                table: "Products");
        }
    }
}
