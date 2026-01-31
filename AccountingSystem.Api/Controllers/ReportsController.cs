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

        public ReportsController(AccountingDbContext context, IPdfService pdfService, ITenantService tenantService, ILedgerService ledgerService)
        {
            _context = context;
            _pdfService = pdfService;
            _tenantService = tenantService;
            _ledgerService = ledgerService;
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
                Description = invoice.Description
            };

            var customerDto = new CustomerDTO { Name = invoice.Customer.Name, Email = invoice.Customer.Email, Phone = invoice.Customer.Phone };
            var companyDto = new CompanyDTO { Name = company.Name, Address = company.Address, TaxId = company.TaxId, Currency = company.Currency };

            var pdfBytes = _pdfService.GenerateInvoicePdf(invoiceDto, companyDto, customerDto);
            return File(pdfBytes, "application/pdf", $"Invoice-{id}.pdf");
        }

        // NEW: Financial Reports Endpoint
        [HttpGet("financials/pdf")]
        public async Task<IActionResult> DownloadFinancialsPdf()
        {
            var tenantId = _tenantService.GetCurrentTenant();
            var company = await _context.Companies.FindAsync(tenantId);
            if (company == null) return BadRequest("Company profile missing.");

            var companyDto = new CompanyDTO { Name = company.Name, Address = company.Address, TaxId = company.TaxId, Currency = company.Currency };

            // Fetch Data
            var tb = await _ledgerService.GetTrialBalanceAsync();
            var accounts = await _ledgerService.GetChartOfAccountsAsync();
            var accountDtos = accounts.Select(a => new AccountDTO { Code = a.Code, Name = a.Name, Type = a.Type }).ToList();

            var pdfBytes = _pdfService.GenerateFinancialReportPdf(tb, accountDtos, companyDto);
            return File(pdfBytes, "application/pdf", $"Financials-{DateTime.Now:yyyy-MM-dd}.pdf");
        }
    }
}