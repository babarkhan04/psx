using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductStoreAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductStoreAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsVisibleOnPos = table.Column<bool>(type: "bit", nullable: false),
                    AssignedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductStoreAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductStoreAssignments_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductStoreAssignments_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductStoreAssignments_ProductId",
                table: "ProductStoreAssignments",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductStoreAssignments_StoreId",
                table: "ProductStoreAssignments",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductStoreAssignments_TenantId_ProductId_StoreId",
                table: "ProductStoreAssignments",
                columns: new[] { "TenantId", "ProductId", "StoreId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductStoreAssignments_TenantId_StoreId_IsVisibleOnPos",
                table: "ProductStoreAssignments",
                columns: new[] { "TenantId", "StoreId", "IsVisibleOnPos" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductStoreAssignments");
        }
    }
}
