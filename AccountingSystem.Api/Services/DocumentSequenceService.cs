using AccountingSystem.API.Data;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Services
{
    public class DocumentSequenceService : IDocumentSequenceService
    {
        private readonly AccountingDbContext _context;
        private const int NumberPadding = 4;

        public DocumentSequenceService(AccountingDbContext context)
        {
            _context = context;
        }

        public async Task<string> GetNextSequenceAsync(int companyId, DocumentType documentType)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var sequence = await _context.DocumentSequences
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.DocumentType == documentType);

                if (sequence == null)
                {
                    sequence = CreateDefaultSequence(companyId, documentType);
                    _context.DocumentSequences.Add(sequence);
                }

                var currentNumber = sequence.NextNumber;
                sequence.NextNumber += 1;

                try
                {
                    await _context.SaveChangesAsync();
                    return $"{sequence.Prefix}{currentNumber.ToString().PadLeft(NumberPadding, '0')}";
                }
                catch (DbUpdateConcurrencyException)
                {
                    _context.ChangeTracker.Clear();
                }
                catch (DbUpdateException)
                {
                    _context.ChangeTracker.Clear();
                }
            }

            throw new InvalidOperationException("Unable to generate document number. Please retry.");
        }

        public async Task<List<DocumentSequenceDTO>> GetSequencesAsync(int companyId)
        {
            await EnsureDefaultSequencesAsync(companyId);

            return await _context.DocumentSequences
                .Where(s => s.CompanyId == companyId)
                .OrderBy(s => s.DocumentType)
                .Select(s => new DocumentSequenceDTO
                {
                    DocumentType = s.DocumentType,
                    Prefix = s.Prefix,
                    NextNumber = s.NextNumber
                })
                .ToListAsync();
        }

        public async Task UpdateSequencesAsync(int companyId, List<UpdateDocumentSequenceDTO> sequences)
        {
            await EnsureDefaultSequencesAsync(companyId);

            foreach (var dto in sequences)
            {
                var sequence = await _context.DocumentSequences
                    .FirstAsync(s => s.CompanyId == companyId && s.DocumentType == dto.DocumentType);
                sequence.Prefix = dto.Prefix.Trim();
                sequence.NextNumber = dto.NextNumber;
            }

            await _context.SaveChangesAsync();
        }

        public async Task EnsureDefaultSequencesAsync(int companyId)
        {
            foreach (var type in Enum.GetValues<DocumentType>())
            {
                var exists = await _context.DocumentSequences
                    .IgnoreQueryFilters()
                    .AnyAsync(s => s.CompanyId == companyId && s.DocumentType == type);

                if (!exists)
                {
                    _context.DocumentSequences.Add(CreateDefaultSequence(companyId, type));
                }
            }

            await _context.SaveChangesAsync();
        }

        private static DocumentSequence CreateDefaultSequence(int companyId, DocumentType documentType)
        {
            return new DocumentSequence
            {
                CompanyId = companyId,
                DocumentType = documentType,
                Prefix = GetDefaultPrefix(documentType),
                NextNumber = 1,
                IsActive = true
            };
        }

        private static string GetDefaultPrefix(DocumentType documentType) => documentType switch
        {
            DocumentType.Invoice => "INV-",
            DocumentType.JournalEntry => "JE-",
            DocumentType.PaymentReceived => "PR-",
            DocumentType.BillPaymentCheck => "CHK-",
            _ => "DOC-"
        };
    }
}
