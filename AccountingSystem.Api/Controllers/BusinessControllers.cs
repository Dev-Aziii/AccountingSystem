using AccountingSystem.API.Data;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Controllers
{
    // --- ACCOUNTS PAYABLE ---
    [ApiController]
    [Route("api/payables")]
    [Authorize(Roles = "Admin,Accounting")]
    public class AccountsPayableController : ControllerBase
    {
        private readonly IPayableService _payableService;
        private readonly AccountingDbContext _context; // Inject Context for list lookups

        public AccountsPayableController(IPayableService payableService, AccountingDbContext context)
        {
            _payableService = payableService;
            _context = context;
        }

        [HttpGet("vendors")]
        public async Task<IActionResult> GetVendors()
        {
            var vendors = await _context.Vendors
                .Select(v => new VendorDTO { Id = v.Id, Name = v.Name })
                .ToListAsync();
            return Ok(vendors);
        }

        [HttpPost("bill")]
        public async Task<IActionResult> CreateBill([FromBody] CreateBillDTO billDto)
        {
            var bill = await _payableService.CreateBillAsync(billDto);
            return Ok(bill);
        }

        [HttpPost("bill/{id}/pay")]
        public async Task<IActionResult> PayBill(int id, [FromBody] ProcessPaymentDTO paymentDto)
        {
            try
            {
                var userId = User.Identity?.Name ?? "Admin";
                var payment = await _payableService.PayBillAsync(id, paymentDto.Amount, paymentDto.PaymentMethod, userId);
                return Ok(payment);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    // --- ACCOUNTS RECEIVABLE ---
    [ApiController]
    [Route("api/receivables")]
    [Authorize(Roles = "Admin,Accounting")]
    public class AccountsReceivableController : ControllerBase
    {
        private readonly IReceivableService _receivableService;
        private readonly AccountingDbContext _context;

        public AccountsReceivableController(IReceivableService receivableService, AccountingDbContext context)
        {
            _receivableService = receivableService;
            _context = context;
        }

        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers()
        {
            var customers = await _context.Customers
                .Select(c => new CustomerDTO { Id = c.Id, Name = c.Name })
                .ToListAsync();
            return Ok(customers);
        }

        [HttpPost("invoice")]
        public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceDTO invoiceDto)
        {
            var invoice = await _receivableService.CreateInvoiceAsync(invoiceDto);
            return Ok(invoice);
        }

        [HttpPost("invoice/{id}/receive")]
        public async Task<IActionResult> ReceivePayment(int id, [FromBody] ProcessPaymentDTO paymentDto)
        {
            try
            {
                var userId = User.Identity?.Name ?? "Admin";
                var payment = await _receivableService.ReceivePaymentAsync(id, paymentDto.Amount, paymentDto.PaymentMethod, userId);
                return Ok(payment);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}