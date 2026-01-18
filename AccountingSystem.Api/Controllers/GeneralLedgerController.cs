using AccountingSystem.API.DTOs;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs; // Updated to use Shared
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/ledger")]
    public class GeneralLedgerController : ControllerBase
    {
        private readonly ILedgerService _ledgerService;

        public GeneralLedgerController(ILedgerService ledgerService)
        {
            _ledgerService = ledgerService;
        }

        // VIEWING: Allowed for Admin, Accounting, and Management
        [HttpGet("accounts")]
        [Authorize(Roles = "Admin,Accounting,Management")]
        public async Task<IActionResult> GetChartOfAccounts()
        {
            var accounts = await _ledgerService.GetChartOfAccountsAsync();
            // Map Entity to Shared DTO
            var dtos = accounts.Select(a => new AccountDTO
            {
                Id = a.Id,
                Code = a.Code,
                Name = a.Name,
                Type = a.Type
            }).ToList();

            return Ok(dtos);
        }

        // VIEWING: Allowed for Admin, Accounting, and Management
        [HttpGet("trial-balance")]
        [Authorize(Roles = "Admin,Accounting,Management")]
        public async Task<IActionResult> GetTrialBalance()
        {
            var tb = await _ledgerService.GetTrialBalanceAsync();
            return Ok(tb);
        }

        // POSTING: Restricted to Admin and Accounting only (Management cannot post)
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