using System.Text.Json.Serialization;

namespace AccountingSystem.Shared.DTOs
{
    // --- REQUESTS ---
    public class CreateSourceDTO
    {
        public decimal Amount { get; set; } // In Pesos (e.g. 100.00)
        public string Description { get; set; } // e.g., "Invoice #1001"
        public string Remarks { get; set; } // Internal Ref ID
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
        public int Amount { get; set; } // In Cents (e.g. 10000)

        [JsonPropertyName("type")]
        public string Type { get; set; } = "gcash"; // Defaulting to GCash for demo

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

        // FIX: Added checkout_url mapping
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