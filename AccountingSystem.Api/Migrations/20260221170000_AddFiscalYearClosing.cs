using System;
using AccountingSystem.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Api.Migrations
{
    [DbContext(typeof(AccountingDbContext))]
    [Migration("20260221170000_AddFiscalYearClosing")]
    /// <inheritdoc />
    public partial class AddFiscalYearClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FiscalYearStartMonth",
                table: "Companies",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "FiscalYearCloses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NetIncome = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosingJournalEntryId = table.Column<int>(type: "int", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYearCloses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalYearCloses_JournalEntries_ClosingJournalEntryId",
                        column: x => x.ClosingJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearCloses_ClosingJournalEntryId",
                table: "FiscalYearCloses",
                column: "ClosingJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearCloses_CompanyId_FiscalYear",
                table: "FiscalYearCloses",
                columns: new[] { "CompanyId", "FiscalYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_CompanyId_Date",
                table: "JournalEntries",
                columns: new[] { "CompanyId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiscalYearCloses");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_CompanyId_Date",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "FiscalYearStartMonth",
                table: "Companies");
        }
    }
}
