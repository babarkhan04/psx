using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerApprovalLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManagerApprovalLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ApprovedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagerApprovalLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManagerApprovalLogs_ApprovedOn",
                table: "ManagerApprovalLogs",
                column: "ApprovedOn");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerApprovalLogs_TenantId_ApprovalType_ApprovedOn",
                table: "ManagerApprovalLogs",
                columns: new[] { "TenantId", "ApprovalType", "ApprovedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ManagerApprovalLogs_TenantId_StoreId_ApprovedOn",
                table: "ManagerApprovalLogs",
                columns: new[] { "TenantId", "StoreId", "ApprovedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManagerApprovalLogs");
        }
    }
}
