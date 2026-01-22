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

            // Note: We use IgnoreQueryFilters because 'Company' table itself doesn't have a CompanyId column 
            // pointing to itself in a way that Global Filters usually handle, OR we simply look it up by ID.
            // Since the user is logged in, we trust the TenantID from the token.

            var company = await _context.Companies.FindAsync(tenantId);

            if (company == null) return NotFound("Company profile not found.");

            return Ok(new CompanyDTO
            {
                Id = company.Id,
                Name = company.Name,
                Address = company.Address,
                TaxId = company.TaxId,
                Currency = company.Currency
            });
        }
    }
}