using AccountingSystem.Shared.DTOs;
using AccountingSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/payables")]
    public class AccountsPayableController : ControllerBase
    {
        private readonly IPayableService _payableService;

        public AccountsPayableController(IPayableService payableService)
        {
            _payableService = payableService;
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
                var payment = await _payableService.PayBillAsync(id, paymentDto.Amount, paymentDto.PaymentMethod, "Admin");
                return Ok(payment);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    [ApiController]
    [Route("api/receivables")]
    public class AccountsReceivableController : ControllerBase
    {
        private readonly IReceivableService _receivableService;

        public AccountsReceivableController(IReceivableService receivableService)
        {
            _receivableService = receivableService;
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
                var payment = await _receivableService.ReceivePaymentAsync(id, paymentDto.Amount, paymentDto.PaymentMethod, "Admin");
                return Ok(payment);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}