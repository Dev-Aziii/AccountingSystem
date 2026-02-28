using AccountingSystem.Shared.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.API.Migrations
{
    public partial class AddDocumentSequences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Invoices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "DocumentSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NextNumber = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSequences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSequences_CompanyId_DocumentType",
                table: "DocumentSequences",
                columns: new[] { "CompanyId", "DocumentType" },
                unique: true);

            migrationBuilder.Sql("UPDATE Invoices SET InvoiceNumber = CONCAT('INV-LEGACY-', Id) WHERE InvoiceNumber = '' OR InvoiceNumber IS NULL;");

            migrationBuilder.Sql(@"
                INSERT INTO DocumentSequences (DocumentType, Prefix, NextNumber, CompanyId, CreatedAt, IsDeleted, IsActive)
                SELECT 'Invoice', 'INV-', 1, c.Id, GETUTCDATE(), 0, 1 FROM Companies c
                WHERE NOT EXISTS (SELECT 1 FROM DocumentSequences ds WHERE ds.CompanyId = c.Id AND ds.DocumentType = 'Invoice');

                INSERT INTO DocumentSequences (DocumentType, Prefix, NextNumber, CompanyId, CreatedAt, IsDeleted, IsActive)
                SELECT 'JournalEntry', 'JE-', 1, c.Id, GETUTCDATE(), 0, 1 FROM Companies c
                WHERE NOT EXISTS (SELECT 1 FROM DocumentSequences ds WHERE ds.CompanyId = c.Id AND ds.DocumentType = 'JournalEntry');

                INSERT INTO DocumentSequences (DocumentType, Prefix, NextNumber, CompanyId, CreatedAt, IsDeleted, IsActive)
                SELECT 'PaymentReceived', 'PR-', 1, c.Id, GETUTCDATE(), 0, 1 FROM Companies c
                WHERE NOT EXISTS (SELECT 1 FROM DocumentSequences ds WHERE ds.CompanyId = c.Id AND ds.DocumentType = 'PaymentReceived');

                INSERT INTO DocumentSequences (DocumentType, Prefix, NextNumber, CompanyId, CreatedAt, IsDeleted, IsActive)
                SELECT 'BillPaymentCheck', 'CHK-', 1, c.Id, GETUTCDATE(), 0, 1 FROM Companies c
                WHERE NOT EXISTS (SELECT 1 FROM DocumentSequences ds WHERE ds.CompanyId = c.Id AND ds.DocumentType = 'BillPaymentCheck');
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DocumentSequences");
            migrationBuilder.DropColumn(name: "InvoiceNumber", table: "Invoices");
        }
    }
}
