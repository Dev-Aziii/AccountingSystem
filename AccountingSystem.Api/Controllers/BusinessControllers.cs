using AccountingSystem.API.Data;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using AccountingSystem.API.Models; // Needed for Entity
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
        private readonly AccountingDbContext _context;

        public AccountsPayableController(IPayableService payableService, AccountingDbContext context)
        {
            _payableService = payableService;
            _context = context;
        }

        // GET LIST
        [HttpGet("vendors")]
        public async Task<IActionResult> GetVendors()
        {
            var vendors = await _context.Vendors
                .Select(v => new VendorDTO
                {
                    Id = v.Id,
                    Name = v.Name,
                    Email = v.Email,
                    ContactPerson = v.ContactPerson
                })
                .ToListAsync();
            return Ok(vendors);
        }

        // CREATE
        [HttpPost("vendors")]
        public async Task<IActionResult> CreateVendor([FromBody] CreateVendorDTO dto)
        {
            var vendor = new Vendor
            {
                Name = dto.Name,
                Email = dto.Email,
                ContactPerson = dto.ContactPerson
            };
            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();

            return Ok(new VendorDTO { Id = vendor.Id, Name = vendor.Name });
        }

        // UPDATE
        [HttpPut("vendors/{id}")]
        public async Task<IActionResult> UpdateVendor(int id, [FromBody] UpdateVendorDTO dto)
        {
            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null) return NotFound("Vendor not found");

            vendor.Name = dto.Name;
            vendor.Email = dto.Email;
            vendor.ContactPerson = dto.ContactPerson;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Vendor updated" });
        }

        // DELETE
        [HttpDelete("vendors/{id}")]
        public async Task<IActionResult> DeleteVendor(int id)
        {
            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null) return NotFound("Vendor not found");

            // Check for existing bills to prevent breaking referential integrity
            bool hasBills = await _context.Bills.AnyAsync(b => b.VendorId == id);
            if (hasBills) return BadRequest(new { error = "Cannot delete vendor with existing bills." });

            _context.Vendors.Remove(vendor);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Vendor deleted" });
        }

        // ... Keep existing Bill endpoints (CreateBill, PayBill) ...
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

    /// --- ACCOUNTS RECEIVABLE ---
    [ApiController]
    [Route("api/receivables")]
    // Note: Management is allowed to view customers based on prompt requirements
    [Authorize(Roles = "Admin,Accounting,Management")]
    public class AccountsReceivableController : ControllerBase
    {
        private readonly IReceivableService _receivableService;
        private readonly AccountingDbContext _context;

        public AccountsReceivableController(IReceivableService receivableService, AccountingDbContext context)
        {
            _receivableService = receivableService;
            _context = context;
        }

        // GET LIST
        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers()
        {
            var customers = await _context.Customers
                .Select(c => new CustomerDTO
                {
                    Id = c.Id,
                    Name = c.Name,
                    Email = c.Email,
                    Phone = c.Phone
                })
                .ToListAsync();
            return Ok(customers);
        }

        // CREATE
        [HttpPost("customers")]
        [Authorize(Roles = "Admin,Accounting")] // Management cannot create
        public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerDTO dto)
        {
            var customer = new Customer
            {
                Name = dto.Name,
                Email = dto.Email,
                Phone = dto.Phone
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            return Ok(new CustomerDTO { Id = customer.Id, Name = customer.Name });
        }

        // UPDATE
        [HttpPut("customers/{id}")]
        [Authorize(Roles = "Admin,Accounting")] // Management cannot edit
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerDTO dto)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound("Customer not found");

            customer.Name = dto.Name;
            customer.Email = dto.Email;
            customer.Phone = dto.Phone;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Customer updated" });
        }

        // DELETE
        [HttpDelete("customers/{id}")]
        [Authorize(Roles = "Admin,Accounting")] // Management cannot delete
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound("Customer not found");

            // Integrity Check
            bool hasInvoices = await _context.Invoices.AnyAsync(i => i.CustomerId == id);
            if (hasInvoices) return BadRequest(new { error = "Cannot delete customer with existing invoices." });

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Customer deleted" });
        }

        // ... Existing Invoice Endpoints ...
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