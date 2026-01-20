using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AccountingSystem.API.Services; // Needed to cast to concrete class if interface isn't updated

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpPost("paymongo-source")]
        [Authorize(Roles = "Admin,Accounting")]
        public async Task<IActionResult> CreateSource([FromBody] CreateSourceDTO request)
        {
            try
            {
                // Cast to concrete PaymentService to access the overload that takes DTO
                // Ideally, update IPaymentService interface to include this method
                if (_paymentService is PaymentService concreteService)
                {
                    var checkoutUrl = await concreteService.CreatePaymentSourceAsync(request);
                    return Ok(new { checkoutUrl });
                }

                // Fallback if casting fails (shouldn't happen with standard DI)
                var url = await _paymentService.CreatePaymentSourceAsync(request.Amount, request.Description, request.Remarks);
                return Ok(new { checkoutUrl = url });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> HandleWebhook()
        {
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();
            var signature = Request.Headers["Paymongo-Signature"].ToString();

            if (!_paymentService.VerifyWebhookSignature(signature, json))
            {
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Received Webhook from PayMongo");
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook processing failed");
                return StatusCode(500);
            }
        }
    }
}