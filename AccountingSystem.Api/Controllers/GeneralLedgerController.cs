using AccountingSystem.Shared.DTOs;
using AccountingSystem.API.Services.Interfaces;
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

        [HttpGet("accounts")]
        public async Task<IActionResult> GetChartOfAccounts()
        {
            var accounts = await _ledgerService.GetChartOfAccountsAsync();
            return Ok(accounts);
        }

        [HttpPost("journal")]
        public async Task<IActionResult> PostJournalEntry([FromBody] JournalEntryDTO entryDto)
        {
            // In real app, get UserId from JWT Claims
            string userId = "Admin";
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

        [HttpGet("trial-balance")]
        public async Task<IActionResult> GetTrialBalance()
        {
            var tb = await _ledgerService.GetTrialBalanceAsync();
            return Ok(tb);
        }
    }
}