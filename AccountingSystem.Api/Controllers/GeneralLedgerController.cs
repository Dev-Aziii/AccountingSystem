using AccountingSystem.API.Data; // Added Context access
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/ledger")]
    public class GeneralLedgerController : ControllerBase
    {
        private readonly ILedgerService _ledgerService;
        private readonly AccountingDbContext _context; // Inject Context

        public GeneralLedgerController(ILedgerService ledgerService, AccountingDbContext context)
        {
            _ledgerService = ledgerService;
            _context = context;
        }

        // GET LIST (Existing)
        [HttpGet("accounts")]
        [Authorize(Roles = "Admin,Accounting,Management")]
        public async Task<IActionResult> GetChartOfAccounts()
        {
            var accounts = await _ledgerService.GetChartOfAccountsAsync();
            var dtos = accounts.Select(a => new AccountDTO
            {
                Id = a.Id,
                Code = a.Code,
                Name = a.Name,
                Type = a.Type
            }).OrderBy(a => a.Code).ToList();

            return Ok(dtos);
        }

        // --- NEW CRUD ENDPOINTS ---

        [HttpPost("accounts")]
        [Authorize(Roles = "Admin,Accounting")]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountDTO dto)
        {
            // Check for duplicate code
            if (await _context.Accounts.AnyAsync(a => a.Code == dto.Code))
            {
                return BadRequest(new { error = $"Account Code '{dto.Code}' already exists." });
            }

            var account = new Account
            {
                Code = dto.Code,
                Name = dto.Name,
                Type = dto.Type
            };
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            return Ok(new AccountDTO { Id = account.Id, Code = account.Code, Name = account.Name, Type = account.Type });
        }

        [HttpPut("accounts/{id}")]
        [Authorize(Roles = "Admin,Accounting")]
        public async Task<IActionResult> UpdateAccount(int id, [FromBody] UpdateAccountDTO dto)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null) return NotFound("Account not found");

            // Prevent changing code if it conflicts
            if (account.Code != dto.Code && await _context.Accounts.AnyAsync(a => a.Code == dto.Code))
            {
                return BadRequest(new { error = $"Account Code '{dto.Code}' already exists." });
            }

            account.Code = dto.Code;
            account.Name = dto.Name;
            account.Type = dto.Type;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Account updated" });
        }

        [HttpDelete("accounts/{id}")]
        [Authorize(Roles = "Admin,Accounting")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null) return NotFound("Account not found");

            // Integrity Check: Cannot delete account if it has journal entries
            bool hasEntries = await _context.JournalEntryLines.AnyAsync(l => l.AccountId == id);
            if (hasEntries) return BadRequest(new { error = "Cannot delete account. It has associated journal entries." });

            _context.Accounts.Remove(account);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Account deleted" });
        }

        // ...  Journal Entry endpoints ...
        [HttpGet("trial-balance")]
        [Authorize(Roles = "Admin,Accounting,Management")]
        public async Task<IActionResult> GetTrialBalance()
        {
            var tb = await _ledgerService.GetTrialBalanceAsync();
            return Ok(tb);
        }

        [HttpPost("journal")]
        [Authorize(Roles = "Admin,Accounting")]
        public async Task<IActionResult> PostJournalEntry([FromBody] JournalEntryDTO entryDto)
        {
            string userId = User.Identity?.Name ?? "Unknown";
            try
            {
                var entry = await _ledgerService.CreateJournalEntryAsync(entryDto, userId);
                return Ok(entry);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}