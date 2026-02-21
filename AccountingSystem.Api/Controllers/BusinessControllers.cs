using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSystem.API.Controllers
{
    // --- ACCOUNTS PAYABLE ---
    [ApiController]
    [Route("api/payables")]
    [Authorize(Roles = "Admin,Accounting")]
    public class AccountsPayableController : ControllerBase
    {
        private readonly IPayableService _payableService;

        public AccountsPayableController(IPayableService payableService)
        {
            _payableService = payableService;
        }

        [HttpGet("vendors")]
        public async Task<IActionResult> GetVendors([FromQuery] bool includeArchived = false)
        {
            var vendors = await _payableService.GetVendorsAsync(includeArchived);
            return Ok(vendors);
        }

        [HttpPost("vendors")]
        public async Task<IActionResult> CreateVendor([FromBody] CreateVendorDTO dto)
        {
            var result = await _payableService.CreateVendorAsync(dto);
            return Ok(result); // Return entity or DTO
        }

        [HttpPut("vendors/{id}")]
        public async Task<IActionResult> UpdateVendor(int id, [FromBody] UpdateVendorDTO dto)
        {
            await _payableService.UpdateVendorAsync(id, dto);
            return Ok(new { message = "Vendor updated" });
        }

        [HttpDelete("vendors/{id}")]
        public async Task<IActionResult> DeleteVendor(int id)
        {
            await _payableService.DeleteVendorAsync(id);
            return Ok(new { message = "Vendor archived" });
        }

        [HttpPut("vendors/{id}/restore")]
        public async Task<IActionResult> RestoreVendor(int id)
        {
            await _payableService.RestoreVendorAsync(id);
            return Ok(new { message = "Vendor restored" });
        }

        [HttpGet("bills")]
        public async Task<IActionResult> GetBills([FromQuery] int? fiscalYear = null, [FromQuery] DocumentStatus? status = null)
        {
            var bills = await _payableService.GetBillsAsync(fiscalYear, status);
            return Ok(bills);
        }

        [HttpPost("bill")]
        public async Task<IActionResult> CreateBill([FromBody] CreateBillDTO billDto)
        {
            var bill = await _payableService.CreateBillAsync(billDto);
            return Ok(bill);
        }

        [HttpPost("bill/{id}/pay")]
        public async Task<IActionResult> PayBill(int id, [FromBody] RecordPaymentDTO paymentDto)
        {
            if (id != paymentDto.ReferenceId) return BadRequest(new { error = "Mismatched Bill ID." });
            var userId = User.Identity?.Name ?? "Admin";
            var payment = await _payableService.PayBillAsync(paymentDto, userId);
            return Ok(payment);
        }
    }

    // --- ACCOUNTS RECEIVABLE ---
    [ApiController]
    [Route("api/receivables")]
    [Authorize(Roles = "Admin,Accounting")]
    public class AccountsReceivableController : ControllerBase
    {
        private readonly IReceivableService _receivableService;

        public AccountsReceivableController(IReceivableService receivableService)
        {
            _receivableService = receivableService;
        }

        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers([FromQuery] bool includeArchived = false)
        {
            var customers = await _receivableService.GetCustomersAsync(includeArchived);
            return Ok(customers);
        }

        [HttpPost("customers")]
        public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerDTO dto)
        {
            var result = await _receivableService.CreateCustomerAsync(dto);
            return Ok(result);
        }

        [HttpPut("customers/{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerDTO dto)
        {
            await _receivableService.UpdateCustomerAsync(id, dto);
            return Ok(new { message = "Customer updated" });
        }

        [HttpDelete("customers/{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            await _receivableService.DeleteCustomerAsync(id);
            return Ok(new { message = "Customer archived" });
        }

        [HttpPut("customers/{id}/restore")]
        public async Task<IActionResult> RestoreCustomer(int id)
        {
            await _receivableService.RestoreCustomerAsync(id);
            return Ok(new { message = "Customer restored" });
        }

        [HttpGet("invoices")]
        public async Task<IActionResult> GetInvoices([FromQuery] int? fiscalYear = null, [FromQuery] DocumentStatus? status = null)
        {
            var invoices = await _receivableService.GetInvoicesAsync(fiscalYear, status);
            return Ok(invoices);
        }

        [HttpPost("invoice")]
        public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceDTO invoiceDto)
        {
            var invoice = await _receivableService.CreateInvoiceAsync(invoiceDto);
            return Ok(invoice);
        }

        [HttpPost("invoice/{id}/receive")]
        public async Task<IActionResult> ReceivePayment(int id, [FromBody] RecordPaymentDTO paymentDto)
        {
            if (id != paymentDto.ReferenceId) return BadRequest(new { error = "Mismatched Invoice ID." });
            var userId = User.Identity?.Name ?? "Admin";
            var payment = await _receivableService.ReceivePaymentAsync(paymentDto, userId);
            return Ok(payment);
        }
    }
}
