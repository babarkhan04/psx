using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurablePosShortcuts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PosShortcutSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ActionName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Ctrl = table.Column<bool>(type: "bit", nullable: false),
                    Alt = table.Column<bool>(type: "bit", nullable: false),
                    Shift = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosShortcutSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PosShortcutSettings_TenantId_ActionCode",
                table: "PosShortcutSettings",
                columns: new[] { "TenantId", "ActionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosShortcutSettings_TenantId_SortOrder",
                table: "PosShortcutSettings",
                columns: new[] { "TenantId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PosShortcutSettings");
        }
    }
}
