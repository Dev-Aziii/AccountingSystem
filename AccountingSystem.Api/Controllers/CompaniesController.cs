using AccountingSystem.API.Data;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/companies")]
    [Authorize] // Requires Login (Tenant Context)
    public class CompaniesController : ControllerBase
    {
        private readonly AccountingDbContext _context;
        private readonly ITenantService _tenantService;

        public CompaniesController(AccountingDbContext context, ITenantService tenantService)
        {
            _context = context;
            _tenantService = tenantService;
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentCompany()
        {
            var tenantId = _tenantService.GetCurrentTenant();

            var company = await _context.Companies.FindAsync(tenantId);

            if (company == null) return NotFound("Company profile not found.");

            return Ok(new CompanyDTO
            {
                Id = company.Id,
                Name = company.Name,
                Address = company.Address ?? string.Empty, 
                TaxId = company.TaxId ?? string.Empty,     
                Currency = company.Currency,
                FiscalYearStartMonth = company.FiscalYearStartMonth
            });
        }

        // Update Company Profile
        [HttpPut("current")]
        [Authorize(Roles = "Admin")] // Only Admins can update company settings
        public async Task<IActionResult> UpdateCompany([FromBody] UpdateCompanyDTO dto)
        {
            var tenantId = _tenantService.GetCurrentTenant();
            var company = await _context.Companies.FindAsync(tenantId);

            if (company == null) return NotFound("Company profile not found.");

            company.Name = dto.Name;
            company.Address = dto.Address;
            company.TaxId = dto.TaxId;
            company.Currency = dto.Currency;
            company.FiscalYearStartMonth = dto.FiscalYearStartMonth;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Company profile updated successfully." });
        }
    }
}
