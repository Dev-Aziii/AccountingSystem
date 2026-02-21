using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
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

        [HttpGet("accounts")]
        [Authorize(Roles = "Admin,Accounting,Management")]
        public async Task<IActionResult> GetChartOfAccounts([FromQuery] bool includeArchived = false)
        {
            var accounts = await _ledgerService.GetChartOfAccountsAsync(includeArchived);
            var dtos = accounts.Select(a => new AccountDTO
            {
                Id = a.Id,
                Code = a.Code,
                Name = a.Name,
                Type = a.Type,
                IsActive = a.IsActive,
                IsDeleted = a.IsDeleted
            }).ToList();

            return Ok(dtos);
        }

        [HttpPost("accounts")]
        [Authorize(Roles = "Admin,Accounting")]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountDTO dto)
        {
            try
            {
                var account = await _ledgerService.CreateAccountAsync(dto);
                return Ok(new AccountDTO { Id = account.Id, Code = account.Code, Name = account.Name, Type = account.Type });
            }
            catch (System.Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpPut("accounts/{id}")]
        [Authorize(Roles = "Admin,Accounting")]
        public async Task<IActionResult> UpdateAccount(int id, [FromBody] UpdateAccountDTO dto)
        {
            try
            {
                await _ledgerService.UpdateAccountAsync(id, dto);
                return Ok(new { message = "Account updated" });
            }
            catch (System.Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpDelete("accounts/{id}")]
        [Authorize(Roles = "Admin,Accounting")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            try
            {
                await _ledgerService.DeleteAccountAsync(id);
                return Ok(new { message = "Account archived" });
            }
            catch (System.Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpPut("accounts/{id}/restore")]
        [Authorize(Roles = "Admin,Accounting")]
        public async Task<IActionResult> RestoreAccount(int id)
        {
            try
            {
                await _ledgerService.RestoreAccountAsync(id);
                return Ok(new { message = "Account restored" });
            }
            catch (System.Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpGet("trial-balance")]
        [Authorize(Roles = "Admin,Accounting,Management")]
        public async Task<IActionResult> GetTrialBalance([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] string view = "post")
        {
            var tb = await _ledgerService.GetTrialBalanceAsync(from, to, !string.Equals(view, "pre", StringComparison.OrdinalIgnoreCase));
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