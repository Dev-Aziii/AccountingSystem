using AccountingSystem.API.Data;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Services
{
    public class YearEndCloseService : IYearEndCloseService
    {
        private readonly AccountingDbContext _context;
        private readonly ITenantService _tenantService;

        public YearEndCloseService(AccountingDbContext context, ITenantService tenantService)
        {
            _context = context;
            _tenantService = tenantService;
        }

        public FiscalPeriod ResolveFiscalPeriod(int fiscalYear)
        {
            var startMonth = GetFiscalYearStartMonth();
            return ResolveFiscalPeriod(fiscalYear, startMonth);
        }

        public async Task EnsurePostingDateIsOpenAsync(DateTime postingDate)
        {
            var postingDay = postingDate.Date;
            var postingDayExclusive = postingDay.AddDays(1);
            var closedPeriod = await _context.FiscalYearCloses
                .AsNoTracking()
                .FirstOrDefaultAsync(f => postingDay >= f.PeriodStart && postingDayExclusive <= f.PeriodEnd.AddDays(1));

            if (closedPeriod != null)
            {
                throw new InvalidOperationException(
                    $"Fiscal year {closedPeriod.FiscalYear} is already closed. Posting date {postingDate:yyyy-MM-dd} is not allowed.");
            }
        }

        public async Task<List<FiscalYearSummaryDTO>> GetFiscalYearSummariesAsync(int lookbackYears = 10)
        {
            if (lookbackYears < 1)
                lookbackYears = 1;

            var startMonth = await GetFiscalYearStartMonthAsync();
            var currentFiscalYear = GetFiscalYearForDate(DateTime.Today, startMonth);

            var closesByYear = await _context.FiscalYearCloses
                .AsNoTracking()
                .ToDictionaryAsync(c => c.FiscalYear);

            var results = new List<FiscalYearSummaryDTO>();

            for (int fiscalYear = currentFiscalYear; fiscalYear >= currentFiscalYear - lookbackYears; fiscalYear--)
            {
                var period = ResolveFiscalPeriod(fiscalYear, startMonth);
                var isClosed = closesByYear.TryGetValue(fiscalYear, out var closeRecord);
                bool hasActivity;
                decimal netIncome;

                if (isClosed && closeRecord != null)
                {
                    hasActivity = true;
                    netIncome = closeRecord.NetIncome;
                }
                else
                {
                    (hasActivity, netIncome) = await GetPeriodActivityAndNetIncomeAsync(period.StartDate, period.EndDate);
                    if (!hasActivity && fiscalYear != currentFiscalYear)
                        continue;
                }

                results.Add(new FiscalYearSummaryDTO
                {
                    FiscalYear = fiscalYear,
                    PeriodStart = closeRecord?.PeriodStart ?? period.StartDate,
                    PeriodEnd = closeRecord?.PeriodEnd ?? period.EndDate,
                    NetIncome = netIncome,
                    HasActivity = hasActivity,
                    IsClosed = isClosed,
                    ClosedAtUtc = closeRecord?.ClosedAtUtc,
                    CanClose = fiscalYear < currentFiscalYear && hasActivity && !isClosed
                });
            }

            return results
                .OrderByDescending(r => r.FiscalYear)
                .ToList();
        }

        public async Task<RunYearEndCloseResultDTO> CloseFiscalYearAsync(int fiscalYear, string userName)
        {
            var startMonth = await GetFiscalYearStartMonthAsync();
            var currentFiscalYear = GetFiscalYearForDate(DateTime.Today, startMonth);

            if (fiscalYear >= currentFiscalYear)
            {
                throw new InvalidOperationException("Only completed fiscal years can be closed.");
            }

            var existing = await _context.FiscalYearCloses
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.FiscalYear == fiscalYear);
            if (existing != null)
            {
                throw new InvalidOperationException($"Fiscal year {fiscalYear} is already closed.");
            }

            var period = ResolveFiscalPeriod(fiscalYear, startMonth);
            var periodEndExclusive = period.EndDate.Date.AddDays(1);

            var accountBalances = await _context.JournalEntryLines
                .Where(l => l.JournalEntry.Date >= period.StartDate && l.JournalEntry.Date < periodEndExclusive)
                .GroupBy(l => new { l.AccountId, l.Account.Type })
                .Select(g => new AccountBalance
                {
                    AccountId = g.Key.AccountId,
                    AccountType = g.Key.Type,
                    Debit = g.Sum(x => x.Debit),
                    Credit = g.Sum(x => x.Credit)
                })
                .ToListAsync();

            var revenueBalances = accountBalances
                .Where(a => string.Equals(a.AccountType, "Revenue", StringComparison.OrdinalIgnoreCase))
                .Select(a => new CloseLineBalance
                {
                    AccountId = a.AccountId,
                    Net = a.Credit - a.Debit
                })
                .Where(a => Math.Abs(a.Net) >= 0.01m)
                .ToList();

            var expenseBalances = accountBalances
                .Where(a => string.Equals(a.AccountType, "Expense", StringComparison.OrdinalIgnoreCase))
                .Select(a => new CloseLineBalance
                {
                    AccountId = a.AccountId,
                    Net = a.Debit - a.Credit
                })
                .Where(a => Math.Abs(a.Net) >= 0.01m)
                .ToList();

            var hasActivity = revenueBalances.Any() || expenseBalances.Any();
            if (!hasActivity)
            {
                throw new InvalidOperationException($"Fiscal year {fiscalYear} has no revenue or expense activity to close.");
            }

            var closingLines = new List<JournalEntryLine>();
            foreach (var revenue in revenueBalances)
            {
                if (revenue.Net > 0)
                {
                    closingLines.Add(new JournalEntryLine
                    {
                        AccountId = revenue.AccountId,
                        Debit = revenue.Net,
                        Credit = 0
                    });
                }
                else
                {
                    closingLines.Add(new JournalEntryLine
                    {
                        AccountId = revenue.AccountId,
                        Debit = 0,
                        Credit = Math.Abs(revenue.Net)
                    });
                }
            }

            foreach (var expense in expenseBalances)
            {
                if (expense.Net > 0)
                {
                    closingLines.Add(new JournalEntryLine
                    {
                        AccountId = expense.AccountId,
                        Debit = 0,
                        Credit = expense.Net
                    });
                }
                else
                {
                    closingLines.Add(new JournalEntryLine
                    {
                        AccountId = expense.AccountId,
                        Debit = Math.Abs(expense.Net),
                        Credit = 0
                    });
                }
            }

            var retainedEarnings = await EnsureRetainedEarningsAccountAsync();

            var totalDebit = closingLines.Sum(l => l.Debit);
            var totalCredit = closingLines.Sum(l => l.Credit);
            var balancingAmount = totalDebit - totalCredit;

            if (Math.Abs(balancingAmount) >= 0.01m)
            {
                closingLines.Add(new JournalEntryLine
                {
                    AccountId = retainedEarnings.Id,
                    Debit = balancingAmount < 0 ? Math.Abs(balancingAmount) : 0,
                    Credit = balancingAmount > 0 ? balancingAmount : 0
                });
            }

            var netIncome = revenueBalances.Sum(r => r.Net) - expenseBalances.Sum(e => e.Net);

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var entry = new JournalEntry
                {
                    Date = period.EndDate,
                    Description = $"System Generated - Closing Entry for Fiscal Year {fiscalYear}",
                    Reference = $"YE-CLOSE-{fiscalYear}",
                    CreatedBy = userName,
                    IsPosted = true,
                    Lines = closingLines
                };

                _context.JournalEntries.Add(entry);
                await _context.SaveChangesAsync();

                var close = new FiscalYearClose
                {
                    FiscalYear = fiscalYear,
                    PeriodStart = period.StartDate,
                    PeriodEnd = period.EndDate,
                    NetIncome = netIncome,
                    ClosingJournalEntryId = entry.Id,
                    ClosedAtUtc = DateTime.UtcNow,
                    ClosedBy = userName
                };

                _context.FiscalYearCloses.Add(close);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();

                return new RunYearEndCloseResultDTO
                {
                    FiscalYear = fiscalYear,
                    ClosingJournalEntryId = entry.Id,
                    NetIncome = netIncome,
                    ClosedAtUtc = close.ClosedAtUtc
                };
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        private async Task<(bool HasActivity, decimal NetIncome)> GetPeriodActivityAndNetIncomeAsync(DateTime periodStart, DateTime periodEnd)
        {
            var periodEndExclusive = periodEnd.Date.AddDays(1);

            var periodData = await _context.JournalEntryLines
                .Where(l => l.JournalEntry.Date >= periodStart && l.JournalEntry.Date < periodEndExclusive)
                .Where(l => !EF.Functions.Like(l.JournalEntry.Reference, "YE-CLOSE-%"))
                .Where(l => l.Account.Type == "Revenue" || l.Account.Type == "Expense")
                .GroupBy(l => l.Account.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    Debit = g.Sum(x => x.Debit),
                    Credit = g.Sum(x => x.Credit)
                })
                .ToListAsync();

            var revenue = periodData
                .Where(x => x.Type == "Revenue")
                .Sum(x => x.Credit - x.Debit);
            var expense = periodData
                .Where(x => x.Type == "Expense")
                .Sum(x => x.Debit - x.Credit);

            var hasActivity = periodData.Any(x => Math.Abs(x.Debit) >= 0.01m || Math.Abs(x.Credit) >= 0.01m);
            return (hasActivity, revenue - expense);
        }

        private async Task<Account> EnsureRetainedEarningsAccountAsync()
        {
            var account = await _context.Accounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Code == "3100");
            if (account != null)
            {
                if (account.IsDeleted || !account.IsActive)
                {
                    account.IsDeleted = false;
                    account.IsActive = true;
                    await _context.SaveChangesAsync();
                }

                return account;
            }

            account = new Account
            {
                CompanyId = _tenantService.GetCurrentTenant(),
                Code = "3100",
                Name = "Retained Earnings",
                Type = "Equity",
                IsActive = true
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();
            return account;
        }

        private async Task<int> GetFiscalYearStartMonthAsync()
        {
            var tenantId = _tenantService.GetCurrentTenant();
            if (tenantId == 0)
                return 1;

            var startMonth = await _context.Companies
                .AsNoTracking()
                .Where(c => c.Id == tenantId)
                .Select(c => (int?)c.FiscalYearStartMonth)
                .FirstOrDefaultAsync();

            return NormalizeStartMonth(startMonth ?? 1);
        }

        private int GetFiscalYearStartMonth()
        {
            var tenantId = _tenantService.GetCurrentTenant();
            if (tenantId == 0)
                return 1;

            var startMonth = _context.Companies
                .AsNoTracking()
                .Where(c => c.Id == tenantId)
                .Select(c => (int?)c.FiscalYearStartMonth)
                .FirstOrDefault();

            return NormalizeStartMonth(startMonth ?? 1);
        }

        private static FiscalPeriod ResolveFiscalPeriod(int fiscalYear, int fiscalYearStartMonth)
        {
            if (fiscalYear < 1)
                throw new InvalidOperationException("Fiscal year must be greater than zero.");

            var startMonth = NormalizeStartMonth(fiscalYearStartMonth);

            DateTime startDate;
            if (startMonth == 1)
            {
                startDate = new DateTime(fiscalYear, 1, 1);
            }
            else
            {
                startDate = new DateTime(fiscalYear - 1, startMonth, 1);
            }

            var endDate = startDate.AddYears(1).AddDays(-1);
            return new FiscalPeriod(fiscalYear, startDate, endDate);
        }

        private static int GetFiscalYearForDate(DateTime date, int fiscalYearStartMonth)
        {
            var startMonth = NormalizeStartMonth(fiscalYearStartMonth);
            if (startMonth == 1)
                return date.Year;

            return date.Month >= startMonth ? date.Year + 1 : date.Year;
        }

        private static int NormalizeStartMonth(int startMonth)
            => startMonth is < 1 or > 12 ? 1 : startMonth;

        private sealed class AccountBalance
        {
            public int AccountId { get; set; }
            public string AccountType { get; set; } = string.Empty;
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
        }

        private sealed class CloseLineBalance
        {
            public int AccountId { get; set; }
            public decimal Net { get; set; }
        }
    }
}
