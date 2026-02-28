using AccountingSystem.API.Models;
using AccountingSystem.Shared.Enums;

namespace AccountingSystem.API.Data
{
    public static class DocumentSequenceSeeder
    {
        public static IEnumerable<DocumentSequence> BuildDefaults(int companyId)
        {
            return new List<DocumentSequence>
            {
                new() { CompanyId = companyId, DocumentType = DocumentType.Invoice, Prefix = "INV-", NextNumber = 1, IsActive = true },
                new() { CompanyId = companyId, DocumentType = DocumentType.JournalEntry, Prefix = "JE-", NextNumber = 1, IsActive = true },
                new() { CompanyId = companyId, DocumentType = DocumentType.PaymentReceived, Prefix = "PR-", NextNumber = 1, IsActive = true },
                new() { CompanyId = companyId, DocumentType = DocumentType.BillPaymentCheck, Prefix = "CHK-", NextNumber = 1, IsActive = true }
            };
        }
    }
}
