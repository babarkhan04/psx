using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptDesignerAndPrintStation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceiptTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PaperSize = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Subtitle = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HeaderText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FooterText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReturnPolicyText = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    ShowStoreName = table.Column<bool>(type: "bit", nullable: false),
                    ShowCustomer = table.Column<bool>(type: "bit", nullable: false),
                    ShowCashier = table.Column<bool>(type: "bit", nullable: false),
                    ShowPaymentSummary = table.Column<bool>(type: "bit", nullable: false),
                    ShowTaxDetails = table.Column<bool>(type: "bit", nullable: false),
                    ShowItemSku = table.Column<bool>(type: "bit", nullable: false),
                    ShowDiscountDetails = table.Column<bool>(type: "bit", nullable: false),
                    ShowPrintedBy = table.Column<bool>(type: "bit", nullable: false),
                    ShowReceiptNumberBox = table.Column<bool>(type: "bit", nullable: false),
                    Copies = table.Column<int>(type: "int", nullable: false),
                    AutoPrintAfterSale = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptTemplates_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrintStationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StationName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ReceiptTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultReceiptMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UseBrowserPrint = table.Column<bool>(type: "bit", nullable: false),
                    AutoPrintReceipt = table.Column<bool>(type: "bit", nullable: false),
                    ReceiptCopies = table.Column<int>(type: "int", nullable: false),
                    ReceiptPrinterName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    LabelPrinterName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    UseLocalPrintAgent = table.Column<bool>(type: "bit", nullable: false),
                    LocalPrintAgentUrl = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    EnableCashDrawerKick = table.Column<bool>(type: "bit", nullable: false),
                    CashDrawerCommandHex = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_PrintStationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintStationSettings_ReceiptTemplates_ReceiptTemplateId",
                        column: x => x.ReceiptTemplateId,
                        principalTable: "ReceiptTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PrintStationSettings_Registers_RegisterId",
                        column: x => x.RegisterId,
                        principalTable: "Registers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrintStationSettings_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrintStationSettings_ReceiptTemplateId",
                table: "PrintStationSettings",
                column: "ReceiptTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintStationSettings_RegisterId",
                table: "PrintStationSettings",
                column: "RegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintStationSettings_StoreId",
                table: "PrintStationSettings",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintStationSettings_TenantId_StoreId_RegisterId",
                table: "PrintStationSettings",
                columns: new[] { "TenantId", "StoreId", "RegisterId" },
                unique: true,
                filter: "[RegisterId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptTemplates_StoreId",
                table: "ReceiptTemplates",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptTemplates_TenantId_Name",
                table: "ReceiptTemplates",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptTemplates_TenantId_StoreId_IsDefault",
                table: "ReceiptTemplates",
                columns: new[] { "TenantId", "StoreId", "IsDefault" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrintStationSettings");

            migrationBuilder.DropTable(
                name: "ReceiptTemplates");
        }
    }
}
