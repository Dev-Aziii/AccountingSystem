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
        private readonly IYearEndCloseService _yearEndCloseService;

        public GeneralLedgerController(ILedgerService ledgerService, IYearEndCloseService yearEndCloseService)
        {
            _ledgerService = ledgerService;
            _yearEndCloseService = yearEndCloseService;
        }

        [HttpGet("accounts")]
        [Authorize(Policy = ApplicationAuthorizationPolicies.RequireTenantOperationalAccess)]
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
        [Authorize(Policy = ApplicationAuthorizationPolicies.RequireTenantAccountingAccess)]
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
        [Authorize(Policy = ApplicationAuthorizationPolicies.RequireTenantAccountingAccess)]
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
        [Authorize(Policy = ApplicationAuthorizationPolicies.RequireTenantAccountingAccess)]
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
        [Authorize(Policy = ApplicationAuthorizationPolicies.RequireTenantAccountingAccess)]
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
        [Authorize(Policy = ApplicationAuthorizationPolicies.RequireTenantOperationalAccess)]
        public async Task<IActionResult> GetTrialBalance(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] bool excludeClosingEntries = false)
        {
            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
                return BadRequest(new { error = "fromDate cannot be later than toDate." });

            var tb = await _ledgerService.GetTrialBalanceAsync(fromDate, toDate, excludeClosingEntries);
            return Ok(tb);
        }

        [HttpGet("fiscal-years")]
        [Authorize(Policy = ApplicationAuthorizationPolicies.RequireTenantOperationalAccess)]
        public async Task<IActionResult> GetFiscalYears([FromQuery] int lookbackYears = 10)
        {
            var years = await _yearEndCloseService.GetFiscalYearSummariesAsync(lookbackYears);
            return Ok(years);
        }

        [HttpPost("fiscal-years/{fiscalYear:int}/close")]
        [Authorize(Policy = ApplicationAuthorizationPolicies.RequireTenantOwner)]
        public async Task<IActionResult> CloseFiscalYear(int fiscalYear)
        {
            var userName = User.Identity?.Name ?? "System";
            try
            {
                var result = await _yearEndCloseService.CloseFiscalYearAsync(fiscalYear, userName);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("already closed", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = ex.Message });

                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("journal")]
        [Authorize(Policy = ApplicationAuthorizationPolicies.RequireTenantAccountingAccess)]
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
