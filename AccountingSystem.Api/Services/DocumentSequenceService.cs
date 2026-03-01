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
                    .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.DocumentType == documentType);

                if (sequence == null)
                {
                    sequence = CreateDefault(companyId, documentType);
                    _context.DocumentSequences.Add(sequence);
                }

                var currentNumber = sequence.NextNumber;
                sequence.NextNumber = currentNumber + 1;

                try
                {
                    await _context.SaveChangesAsync();
                    return $"{sequence.Prefix}{currentNumber:D4}";
                }
                catch (DbUpdateConcurrencyException)
                {
                    _context.ChangeTracker.Clear();
                }
                catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _context.ChangeTracker.Clear();
                }
            }

            throw new InvalidOperationException("Unable to generate document reference due to high contention.");
        }

        public async Task<List<DocumentSequenceDTO>> GetSequencesAsync(int companyId)
        {
            var existing = await _context.DocumentSequences
                .IgnoreQueryFilters()
                .Where(x => x.CompanyId == companyId)
                .ToListAsync();

            var result = new List<DocumentSequenceDTO>();
            foreach (var type in Enum.GetValues<DocumentType>())
            {
                var row = existing.FirstOrDefault(x => x.DocumentType == type) ?? CreateDefault(companyId, type);
                result.Add(new DocumentSequenceDTO { DocumentType = type, Prefix = row.Prefix, NextNumber = row.NextNumber });
            }

            return result.OrderBy(x => x.DocumentType).ToList();
        }

        public async Task<DocumentSequenceDTO> UpsertSequenceAsync(int companyId, UpdateDocumentSequenceDTO dto)
        {
            var sequence = await _context.DocumentSequences
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.DocumentType == dto.DocumentType);

            if (sequence == null)
            {
                sequence = CreateDefault(companyId, dto.DocumentType);
                _context.DocumentSequences.Add(sequence);
            }

            sequence.Prefix = dto.Prefix.Trim().ToUpperInvariant();
            sequence.NextNumber = dto.NextNumber;

            await _context.SaveChangesAsync();

            return new DocumentSequenceDTO
            {
                DocumentType = sequence.DocumentType,
                Prefix = sequence.Prefix,
                NextNumber = sequence.NextNumber
            };
        }

        private static DocumentSequence CreateDefault(int companyId, DocumentType type)
            => new()
            {
                CompanyId = companyId,
                DocumentType = type,
                Prefix = type switch
                {
                    DocumentType.Invoice => "INV-",
                    DocumentType.JournalEntry => "JE-",
                    DocumentType.PaymentReceived => "PR-",
                    DocumentType.CheckPayment => "CHK-",
                    _ => "DOC-"
                },
                NextNumber = 1
            };
    }
}
