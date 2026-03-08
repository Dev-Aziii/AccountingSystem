using AccountingSystem.API.Data;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly AccountingDbContext _context;
        private readonly IPdfService _pdfService;
        private readonly ITenantService _tenantService;
        private readonly ILedgerService _ledgerService; // Needed for TB & Accounts
        private readonly IYearEndCloseService _yearEndCloseService;

        public ReportsController(
            AccountingDbContext context,
            IPdfService pdfService,
            ITenantService tenantService,
            ILedgerService ledgerService,
            IYearEndCloseService yearEndCloseService)
        {
            _context = context;
            _pdfService = pdfService;
            _tenantService = tenantService;
            _ledgerService = ledgerService;
            _yearEndCloseService = yearEndCloseService;
        }

        [HttpGet("invoices/{id}/pdf")]
        public async Task<IActionResult> DownloadInvoicePdf(int id)
        {
            var invoice = await _context.Invoices.Include(i => i.Customer).FirstOrDefaultAsync(i => i.Id == id);
            if (invoice == null) return NotFound("Invoice not found");

            var tenantId = _tenantService.GetCurrentTenant();
            var company = await _context.Companies.FindAsync(tenantId);
            if (company == null) return BadRequest("Company profile missing.");

            var invoiceDto = new InvoiceDTO
            {
                Id = invoice.Id,
                DueDate = invoice.DueDate,
                TotalAmount = invoice.TotalAmount,
                PaidAmount = invoice.PaidAmount,
                Status = invoice.Status,
                Description = invoice.Description ?? string.Empty
            };

            var customerDto = new CustomerDTO
            {
                Name = invoice.Customer?.Name ?? string.Empty,
                Email = invoice.Customer?.Email ?? string.Empty,
                Phone = invoice.Customer?.Phone ?? string.Empty
            };

            var companyDto = new CompanyDTO
            {
                Name = company.Name ?? string.Empty,
                Address = company.Address ?? string.Empty,
                TaxId = company.TaxId ?? string.Empty,
                Currency = company.Currency ?? string.Empty
            };

            var pdfBytes = _pdfService.GenerateInvoicePdf(invoiceDto, companyDto, customerDto);
            return File(pdfBytes, "application/pdf", $"Invoice-{id}.pdf");
        }

        // Financial Reports Endpoint
        [HttpGet("financials/pdf")]
        public async Task<IActionResult> DownloadFinancialsPdf([FromQuery] int fiscalYear)
        {
            if (fiscalYear <= 0) return BadRequest("A valid fiscalYear query parameter is required.");

            var tenantId = _tenantService.GetCurrentTenant();
            var company = await _context.Companies.FindAsync(tenantId);
            if (company == null) return BadRequest("Company profile missing.");

            var companyDto = new CompanyDTO
            {
                Name = company.Name ?? string.Empty,
                Address = company.Address ?? string.Empty,
                TaxId = company.TaxId ?? string.Empty,
                Currency = company.Currency ?? string.Empty
            };

            var period = _yearEndCloseService.ResolveFiscalPeriod(fiscalYear);

            // Fetch Data
            var incomeTb = await _ledgerService.GetTrialBalanceAsync(period.StartDate, period.EndDate, excludeClosingEntries: true);
            var balanceTb = await _ledgerService.GetTrialBalanceAsync(null, period.EndDate);
            var accounts = await _ledgerService.GetChartOfAccountsAsync();
            var accountDtos = accounts.Select(a => new AccountDTO
            {
                Code = a.Code ?? string.Empty,
                Name = a.Name ?? string.Empty,
                Type = a.Type ?? string.Empty
            }).ToList();

            var pdfBytes = _pdfService.GenerateFinancialReportPdf(
                incomeTb,
                balanceTb,
                accountDtos,
                companyDto,
                period.StartDate,
                period.EndDate);

            return File(pdfBytes, "application/pdf", $"Financials-FY{fiscalYear}.pdf");
        }
    }
}
