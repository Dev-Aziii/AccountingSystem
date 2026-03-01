using AccountingSystem.API.Data;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Services
{
    public class LedgerService : ILedgerService
    {
        private readonly AccountingDbContext _context;
        private readonly IYearEndCloseService _yearEndCloseService;
        private readonly ITenantService _tenantService;
        private readonly IDocumentSequenceService _documentSequenceService;

        public LedgerService(
            AccountingDbContext context,
            IYearEndCloseService yearEndCloseService,
            ITenantService tenantService,
            IDocumentSequenceService documentSequenceService)
        {
            _context = context;
            _yearEndCloseService = yearEndCloseService;
            _tenantService = tenantService;
            _documentSequenceService = documentSequenceService;
        }

        public async Task<List<Account>> GetChartOfAccountsAsync(bool includeArchived = false)
        {
            var query = _context.Accounts.AsQueryable();
            if (includeArchived) query = query.IgnoreQueryFilters();
            return await query.OrderBy(a => a.Code).ToListAsync();
        }

        public async Task<JournalEntry> CreateJournalEntryAsync(JournalEntryDTO entryDto, string userId, bool saveImmediately = true)
        {
            var totalDebit = entryDto.Lines.Sum(l => l.Debit);
            var totalCredit = entryDto.Lines.Sum(l => l.Credit);

            if (totalDebit != totalCredit)
                throw new InvalidOperationException($"Transaction is not balanced. Debit: {totalDebit}, Credit: {totalCredit}");

            await _yearEndCloseService.EnsurePostingDateIsOpenAsync(entryDto.Date);

            var companyId = _tenantService.GetCurrentTenant();
            var referenceNumber = await _documentSequenceService.GetNextSequenceAsync(companyId, DocumentType.JournalEntry);

            var entry = new JournalEntry
            {
                Date = entryDto.Date,
                Description = entryDto.Description,
                Reference = referenceNumber,
                CreatedBy = userId,
                IsPosted = true,
                Lines = entryDto.Lines.Select(l => new JournalEntryLine
                {
                    AccountId = l.AccountId,
                    Debit = l.Debit,
                    Credit = l.Credit
                }).ToList()
            };

            _context.JournalEntries.Add(entry);
            if (saveImmediately)
            {
                await _context.SaveChangesAsync();
            }

            return entry;
        }

        public async Task<TrialBalanceDTO> GetTrialBalanceAsync(DateTime? fromDate = null, DateTime? toDate = null, bool excludeClosingEntries = false)
        {
            var linesQuery = _context.JournalEntryLines.AsQueryable();
            if (fromDate.HasValue) linesQuery = linesQuery.Where(l => l.JournalEntry.Date >= fromDate.Value.Date);
            if (toDate.HasValue) linesQuery = linesQuery.Where(l => l.JournalEntry.Date < toDate.Value.Date.AddDays(1));
            if (excludeClosingEntries) linesQuery = linesQuery.Where(l => !EF.Functions.Like(l.JournalEntry.Reference, "YE-CLOSE-%"));

            var balances = await linesQuery
                .GroupBy(l => new { l.Account.Code, l.Account.Name })
                .Select(g => new AccountBalanceDTO
                {
                    AccountCode = g.Key.Code,
                    AccountName = g.Key.Name,
                    Debit = g.Sum(x => x.Debit),
                    Credit = g.Sum(x => x.Credit)
                })
                .ToListAsync();

            return new TrialBalanceDTO
            {
                GeneratedAt = DateTime.UtcNow,
                Accounts = balances,
                TotalDebit = balances.Sum(x => x.Debit),
                TotalCredit = balances.Sum(x => x.Credit)
            };
        }

        public async Task<Account> CreateAccountAsync(CreateAccountDTO dto)
        {
            if (await _context.Accounts.AnyAsync(a => a.Code == dto.Code))
                throw new Exception($"Account Code '{dto.Code}' already exists.");

            var account = new Account { Code = dto.Code, Name = dto.Name, Type = dto.Type, IsActive = true };
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();
            return account;
        }

        public async Task UpdateAccountAsync(int id, UpdateAccountDTO dto)
        {
            var account = await _context.Accounts.FindAsync(id) ?? throw new Exception("Account not found");
            if (account.Code != dto.Code && await _context.Accounts.AnyAsync(a => a.Code == dto.Code))
                throw new Exception($"Account Code '{dto.Code}' already exists.");
            account.Code = dto.Code;
            account.Name = dto.Name;
            account.Type = dto.Type;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAccountAsync(int id)
        {
            var account = await _context.Accounts.FindAsync(id) ?? throw new Exception("Account not found");
            if (await _context.JournalEntryLines.AnyAsync(l => l.AccountId == id))
                throw new Exception("Cannot delete account. It has associated journal entries.");
            account.IsDeleted = true;
            account.IsActive = false;
            await _context.SaveChangesAsync();
        }

        public async Task RestoreAccountAsync(int id)
        {
            var account = await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id) ?? throw new Exception("Account not found");
            account.IsDeleted = false;
            account.IsActive = true;
            await _context.SaveChangesAsync();
        }
    }
}
