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

        [HttpPost("paymongo-source")]
        [Authorize(Policy = ApplicationAuthorizationPolicies.RequireTenantAccountingAccess)]
        public async Task<ActionResult<PaymentSourceResponseDTO>> CreateSource(
            [FromBody] CreateSourceDTO request)
        {
            try
            {
                var result = await _paymentService.CreatePaymentSourceAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayMongo source creation failed");
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
                return Unauthorized();

            _logger.LogInformation("Received Webhook from PayMongo");
            return Ok();
        }
    }
}
