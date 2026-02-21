using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/fiscal-years")]
    [Authorize(Roles = "Admin,Accounting,Management")]
    public class FiscalYearsController : ControllerBase
    {
        private readonly IFiscalYearService _fiscalYearService;

        public FiscalYearsController(IFiscalYearService fiscalYearService)
        {
            _fiscalYearService = fiscalYearService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _fiscalYearService.GetFiscalYearsAsync());

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent()
        {
            var current = await _fiscalYearService.GetCurrentFiscalYearAsync();
            return current == null ? NotFound() : Ok(current);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateFiscalYearDTO dto)
        {
            try { return Ok(await _fiscalYearService.CreateFiscalYearAsync(dto)); }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpPost("{id}/close")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CloseYear(int id)
        {
            try
            {
                int? userId = null;
                var claim = User.FindFirst("UserId")?.Value;
                if (int.TryParse(claim, out var parsed)) userId = parsed;
                return Ok(await _fiscalYearService.CloseFiscalYearAsync(id, userId));
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
        }
    }
}
