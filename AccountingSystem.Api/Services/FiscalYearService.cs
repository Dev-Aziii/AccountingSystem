using AccountingSystem.API.Data;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Services
{
    public class FiscalYearService : IFiscalYearService
    {
        private readonly AccountingDbContext _context;

        public FiscalYearService(AccountingDbContext context)
        {
            _context = context;
        }

        public async Task<List<FiscalYearDTO>> GetFiscalYearsAsync()
        {
            return await _context.FiscalYears.OrderByDescending(f => f.StartDate)
                .Select(f => new FiscalYearDTO
                {
                    Id = f.Id,
                    Name = f.Name,
                    StartDate = f.StartDate,
                    EndDate = f.EndDate,
                    IsClosed = f.IsClosed,
                    ClosedAt = f.ClosedAt,
                    ClosedByUserId = f.ClosedByUserId
                }).ToListAsync();
        }

        public async Task<FiscalYearDTO?> GetCurrentFiscalYearAsync()
        {
            var today = DateTime.UtcNow.Date;
            var fy = await _context.FiscalYears.FirstOrDefaultAsync(f => f.StartDate <= today && f.EndDate >= today);
            return fy == null ? null : Map(fy);
        }

        public async Task<FiscalYearDTO> CreateFiscalYearAsync(CreateFiscalYearDTO dto)
        {
            if (dto.EndDate < dto.StartDate) throw new Exception("EndDate cannot be before StartDate.");
            var overlap = await _context.FiscalYears.AnyAsync(f => dto.StartDate <= f.EndDate && dto.EndDate >= f.StartDate);
            if (overlap) throw new Exception("Fiscal year overlaps an existing period.");

            var fy = new FiscalYear
            {
                Name = dto.Name,
                StartDate = dto.StartDate.Date,
                EndDate = dto.EndDate.Date,
                IsClosed = false
            };
            _context.FiscalYears.Add(fy);
            await _context.SaveChangesAsync();
            return Map(fy);
        }

        public async Task EnsureDateOpenAsync(DateTime date)
        {
            var txDate = date.Date;
            var closed = await _context.FiscalYears.AnyAsync(f => f.IsClosed && txDate >= f.StartDate && txDate <= f.EndDate);
            if (closed) throw new Exception("Transaction date falls within a closed fiscal year.");
        }

        public async Task<FiscalYearDTO> CloseFiscalYearAsync(int fiscalYearId, int? userId)
        {
            var fy = await _context.FiscalYears.FirstOrDefaultAsync(f => f.Id == fiscalYearId) ?? throw new Exception("Fiscal year not found.");
            if (fy.IsClosed) throw new Exception("Fiscal year already closed.");

            var hasDrafts = await _context.JournalEntries.AnyAsync(j => !j.IsPosted && j.Date >= fy.StartDate && j.Date <= fy.EndDate);
            if (hasDrafts) throw new Exception("Cannot close fiscal year with unposted draft journals.");

            var retained = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "3100")
                ?? throw new Exception("Retained Earnings account (3100) is required.");

            var lines = await _context.JournalEntryLines
                .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.Date >= fy.StartDate && l.JournalEntry.Date <= fy.EndDate
                    && (l.Account.Type == "Revenue" || l.Account.Type == "Expense"))
                .GroupBy(l => new { l.AccountId, l.Account.Type })
                .Select(g => new
                {
                    g.Key.AccountId,
                    g.Key.Type,
                    Net = g.Sum(x => x.Debit - x.Credit)
                }).ToListAsync();

            var closingLines = new List<JournalEntryLineDTO>();
            foreach (var item in lines.Where(x => x.Net != 0))
            {
                if (item.Net > 0)
                {
                    closingLines.Add(new JournalEntryLineDTO { AccountId = item.AccountId, Debit = 0, Credit = item.Net });
                }
                else
                {
                    closingLines.Add(new JournalEntryLineDTO { AccountId = item.AccountId, Debit = -item.Net, Credit = 0 });
                }
            }

            if (closingLines.Any())
            {
                var totalDebit = closingLines.Sum(l => l.Debit);
                var totalCredit = closingLines.Sum(l => l.Credit);
                if (totalDebit > totalCredit)
                    closingLines.Add(new JournalEntryLineDTO { AccountId = retained.Id, Debit = 0, Credit = totalDebit - totalCredit });
                else if (totalCredit > totalDebit)
                    closingLines.Add(new JournalEntryLineDTO { AccountId = retained.Id, Debit = totalCredit - totalDebit, Credit = 0 });

                _context.JournalEntries.Add(new JournalEntry
                {
                    Date = fy.EndDate,
                    Description = $"Fiscal year closing entry - {fy.Name}",
                    Reference = $"FY-CLOSE-{fy.Name}",
                    IsPosted = true,
                    IsSystemGenerated = true,
                    CreatedBy = "System",
                    Lines = closingLines.Select(l => new JournalEntryLine
                    {
                        AccountId = l.AccountId,
                        Debit = l.Debit,
                        Credit = l.Credit
                    }).ToList()
                });
            }

            fy.IsClosed = true;
            fy.ClosedAt = DateTime.UtcNow;
            fy.ClosedByUserId = userId;

            var next = await _context.FiscalYears.FirstOrDefaultAsync(f => f.StartDate == fy.EndDate.AddDays(1));
            if (next != null)
            {
                await CreateOpeningBalanceAsync(fy, next);
            }

            await _context.SaveChangesAsync();
            return Map(fy);
        }

        private async Task CreateOpeningBalanceAsync(FiscalYear closedYear, FiscalYear nextYear)
        {
            var balances = await _context.JournalEntryLines
                .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.Date <= closedYear.EndDate
                    && (l.Account.Type == "Asset" || l.Account.Type == "Liability" || l.Account.Type == "Equity"))
                .GroupBy(l => new { l.AccountId, l.Account.Type })
                .Select(g => new { g.Key.AccountId, Net = g.Sum(x => x.Debit - x.Credit) })
                .ToListAsync();

            var openingLines = new List<JournalEntryLine>();
            foreach (var b in balances.Where(x => x.Net != 0))
            {
                openingLines.Add(new JournalEntryLine
                {
                    AccountId = b.AccountId,
                    Debit = b.Net > 0 ? b.Net : 0,
                    Credit = b.Net < 0 ? -b.Net : 0
                });
            }

            if (!openingLines.Any()) return;

            var td = openingLines.Sum(x => x.Debit);
            var tc = openingLines.Sum(x => x.Credit);
            if (td != tc)
            {
                var retained = await _context.Accounts.FirstAsync(a => a.Code == "3100");
                if (td > tc) openingLines.Add(new JournalEntryLine { AccountId = retained.Id, Debit = 0, Credit = td - tc });
                if (tc > td) openingLines.Add(new JournalEntryLine { AccountId = retained.Id, Debit = tc - td, Credit = 0 });
            }

            var exists = await _context.JournalEntries.AnyAsync(j => j.Reference == $"FY-OPEN-{nextYear.Name}");
            if (!exists)
            {
                _context.JournalEntries.Add(new JournalEntry
                {
                    Date = nextYear.StartDate,
                    Description = $"Opening balance entry - {nextYear.Name}",
                    Reference = $"FY-OPEN-{nextYear.Name}",
                    IsPosted = true,
                    IsSystemGenerated = true,
                    CreatedBy = "System",
                    Lines = openingLines
                });
            }
        }

        private static FiscalYearDTO Map(FiscalYear f) => new()
        {
            Id = f.Id,
            Name = f.Name,
            StartDate = f.StartDate,
            EndDate = f.EndDate,
            IsClosed = f.IsClosed,
            ClosedAt = f.ClosedAt,
            ClosedByUserId = f.ClosedByUserId
        };
    }
}
