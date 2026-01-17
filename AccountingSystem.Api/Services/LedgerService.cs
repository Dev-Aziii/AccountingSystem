using AccountingSystem.API.Data;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services.Interfaces;
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

        public async Task<List<Account>> GetChartOfAccountsAsync()
        {
            return await _context.Accounts.ToListAsync();
        }

        public async Task<JournalEntry> CreateJournalEntryAsync(JournalEntryDTO entryDto, string userId)
        {
            // 1. Validate Double Entry Rule
            var totalDebit = entryDto.Lines.Sum(l => l.Debit);
            var totalCredit = entryDto.Lines.Sum(l => l.Credit);

            if (totalDebit != totalCredit)
                throw new InvalidOperationException($"Transaction is not balanced. Debit: {totalDebit}, Credit: {totalCredit}");

            // 2. Create Entry
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

            // 3. Update Account Balances (Simple caching logic)
            foreach (var line in entryDto.Lines)
            {
                var account = await _context.Accounts.FindAsync(line.AccountId);
                if (account != null)
                {
                    // Logic depends on account type (Asset/Exp increases on Dr, Liab/Eq/Rev increases on Cr)
                    // Simplification: Store net balance. 
                    // Production app would calculate strictly based on Type.
                    // For now, we assume simple addition/subtraction visualization in reports
                }
            }

            _context.JournalEntries.Add(entry);
            await _context.SaveChangesAsync();
            return entry;
        }

        public async Task<TrialBalanceDTO> GetTrialBalanceAsync()
        {
            // Aggregate all lines grouped by Account
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
    }
}