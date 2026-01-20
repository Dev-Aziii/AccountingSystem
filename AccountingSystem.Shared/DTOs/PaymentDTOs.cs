using System.Text.Json.Serialization;

namespace AccountingSystem.Shared.DTOs
{
    // --- INTERNAL PAYMENT RECORDING ---
    public class RecordPaymentDTO
    {
        public int ReferenceId { get; set; } // InvoiceId or BillId
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.Now;
        public string PaymentMethod { get; set; } = string.Empty;

        // FIX: Made nullable to prevent "Field is Required" validation errors on empty inputs
        public string? ReferenceNumber { get; set; }
        public int AssetAccountId { get; set; }
        public string? Remarks { get; set; }

        // FIX: Made nullable because Cash/Check payments won't have a PayMongo SourceId
        public string? SourceId { get; set; }
    }

    public class PaymentHistoryDTO
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; }
        public string? ReferenceNumber { get; set; }
        public string AccountName { get; set; }
    }

    // --- REQUESTS ---
    public class CreateSourceDTO
    {
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public string? Remarks { get; set; }
        public string? SuccessUrl { get; set; }
        public string? FailedUrl { get; set; }
    }

    // --- PAYMONGO API MODELS ---
    public class PayMongoSourceRequest
    {
        [JsonPropertyName("data")]
        public SourceData Data { get; set; }
    }

    public class SourceData
    {
        [JsonPropertyName("attributes")]
        public SourceAttributes Attributes { get; set; }
    }

    public class SourceAttributes
    {
        [JsonPropertyName("amount")]
        public int Amount { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "gcash";

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "PHP";

        [JsonPropertyName("redirect")]
        public RedirectUrls Redirect { get; set; }

        [JsonPropertyName("billing")]
        public BillingInfo Billing { get; set; }
    }

    public class RedirectUrls
    {
        [JsonPropertyName("success")]
        public string Success { get; set; }

        [JsonPropertyName("failed")]
        public string Failed { get; set; }

        [JsonPropertyName("checkout_url")]
        public string CheckoutUrl { get; set; }
    }

    public class BillingInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("email")]
        public string Email { get; set; }
    }

    // --- RESPONSES ---
    public class PayMongoSourceResponse
    {
        [JsonPropertyName("data")]
        public ResponseData Data { get; set; }
    }

    public class ResponseData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("attributes")]
        public ResponseAttributes Attributes { get; set; }
    }

    public class ResponseAttributes
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("redirect")]
        public RedirectUrls Redirect { get; set; }
    }

    // --- WEBHOOKS ---
    public class PayMongoWebhookEvent
    {
        [JsonPropertyName("data")]
        public WebhookData Data { get; set; }
    }

    public class WebhookData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("attributes")]
        public WebhookAttributes Attributes { get; set; }
    }

    public class WebhookAttributes
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("data")]
        public ResponseData Data { get; set; }
    }
}