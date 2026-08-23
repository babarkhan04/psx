using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNumberSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NumberSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SequenceDate = table.Column<DateTime>(type: "date", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CurrentNumber = table.Column<long>(type: "bigint", nullable: false),
                    Padding = table.Column<int>(type: "int", nullable: false),
                    LastGeneratedNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LastGeneratedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_NumberSequences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NumberSequences_StoreId_SequenceType_SequenceDate",
                table: "NumberSequences",
                columns: new[] { "StoreId", "SequenceType", "SequenceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberSequences_TenantId_StoreId_SequenceType_SequenceDate",
                table: "NumberSequences",
                columns: new[] { "TenantId", "StoreId", "SequenceType", "SequenceDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NumberSequences");
        }
    }
}
