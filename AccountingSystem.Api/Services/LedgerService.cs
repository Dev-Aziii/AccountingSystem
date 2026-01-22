using AccountingSystem.API.Data;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Services
{
    public class LedgerService : ILedgerService
    {
        private readonly AccountingDbContext _context;

        public LedgerService(AccountingDbContext context)
        {
            _context = context;
        }

        public async Task<List<Account>> GetChartOfAccountsAsync(bool includeArchived = false)
        {
            var query = _context.Accounts.AsQueryable();

            if (includeArchived)
            {
                query = query.IgnoreQueryFilters();
            }

            return await query.OrderBy(a => a.Code).ToListAsync();
        }

        public async Task<JournalEntry> CreateJournalEntryAsync(JournalEntryDTO entryDto, string userId)
        {
            var totalDebit = entryDto.Lines.Sum(l => l.Debit);
            var totalCredit = entryDto.Lines.Sum(l => l.Credit);

            if (totalDebit != totalCredit)
                throw new InvalidOperationException($"Transaction is not balanced. Debit: {totalDebit}, Credit: {totalCredit}");

            var entry = new JournalEntry
            {
                Date = entryDto.Date,
                Description = entryDto.Description,
                Reference = entryDto.Reference,
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
            await _context.SaveChangesAsync();
            return entry;
        }

        public async Task<TrialBalanceDTO> GetTrialBalanceAsync()
        {
            var balances = await _context.JournalEntryLines
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

        // --- Account CRUD ---
        public async Task<Account> CreateAccountAsync(CreateAccountDTO dto)
        {
            if (await _context.Accounts.AnyAsync(a => a.Code == dto.Code))
                throw new Exception($"Account Code '{dto.Code}' already exists.");

            var account = new Account
            {
                Code = dto.Code,
                Name = dto.Name,
                Type = dto.Type,
                IsActive = true
            };
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();
            return account;
        }

        public async Task UpdateAccountAsync(int id, UpdateAccountDTO dto)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null) throw new Exception("Account not found");

            if (account.Code != dto.Code && await _context.Accounts.AnyAsync(a => a.Code == dto.Code))
                throw new Exception($"Account Code '{dto.Code}' already exists.");

            account.Code = dto.Code;
            account.Name = dto.Name;
            account.Type = dto.Type;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAccountAsync(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null) throw new Exception("Account not found");

            if (await _context.JournalEntryLines.AnyAsync(l => l.AccountId == id))
                throw new Exception("Cannot delete account. It has associated journal entries.");

            // Soft Delete logic
            account.IsDeleted = true;
            account.IsActive = false;
            await _context.SaveChangesAsync();
        }

        public async Task RestoreAccountAsync(int id)
        {
            var account = await _context.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id);
            if (account == null) throw new Exception("Account not found");

            account.IsDeleted = false;
            account.IsActive = true;
            await _context.SaveChangesAsync();
        }
    }
}