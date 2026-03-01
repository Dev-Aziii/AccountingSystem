using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentSequencesAndDocumentReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ReferenceNumber",
                table: "Bills",
                newName: "VendorReferenceNumber");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "Invoices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SystemReferenceNumber",
                table: "Bills",
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
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NextNumber = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentSequences");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SystemReferenceNumber",
                table: "Bills");

            migrationBuilder.RenameColumn(
                name: "VendorReferenceNumber",
                table: "Bills",
                newName: "ReferenceNumber");
        }
    }
}
