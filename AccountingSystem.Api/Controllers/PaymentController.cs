using AccountingSystem.Shared.DTOs;
using AccountingSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSystem.API.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IReceivableService _receivableService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentService paymentService,
            IReceivableService receivableService,
            ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _receivableService = receivableService;
            _logger = logger;
        }

        // 1. Initiate Payment (User clicks "Pay via GCash")
        [HttpPost("paymongo-source")]
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

        // 2. Webhook Listener (PayMongo calls this)
        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            // Read stream
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();
            var signature = Request.Headers["Paymongo-Signature"].ToString();

            if (!_paymentService.VerifyWebhookSignature(signature, json))
            {
                return Unauthorized();
            }

            try
            {
                // Parse Event
                var webhookEvent = System.Text.Json.JsonSerializer.Deserialize<PayMongoWebhookEvent>(json);

                if (webhookEvent?.Data?.Attributes?.Type == "source.chargeable")
                {
                    // Logic: source is ready to be charged (or paid in simpler flow)
                    // In a full implementation, you would now call the "create payment" endpoint of PayMongo
                    // OR if this event means money is received:

                    _logger.LogInformation($"Payment Source Chargeable: {webhookEvent.Data.Id}");

                    // AUTOMATION: Find the Invoice ID from the internal database based on the Source ID mapping
                    // and call _receivableService.ReceivePaymentAsync(...)
                    // For this demo, we just log it.
                }

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