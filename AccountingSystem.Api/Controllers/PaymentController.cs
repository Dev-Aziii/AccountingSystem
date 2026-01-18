using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentService paymentService,
            ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        // INITIATE PAYMENT: Internal users only
        [HttpPost("paymongo-source")]
        [Authorize(Roles = "Admin,Accounting")]
        public async Task<IActionResult> CreateSource([FromBody] CreateSourceDTO request)
        {
            try
            {
                var checkoutUrl = await _paymentService.CreatePaymentSourceAsync(request.Amount, request.Description, request.Remarks);
                return Ok(new { checkoutUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // WEBHOOK: MUST BE PUBLIC (AllowAnonymous)
        // PayMongo servers call this, and they do not have our JWT.
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
                // Webhook logic (Step 8 Phase 2)
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