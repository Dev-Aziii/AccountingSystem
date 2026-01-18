using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using System.Text;
using System.Text.Json;

namespace AccountingSystem.API.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public PaymentService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();

            // Set Base URL
            _httpClient.BaseAddress = new Uri("https://api.paymongo.com/v1/");

            var secretKey = _configuration["PayMongo:SecretKey"];
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(secretKey + ":"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
        }

        public async Task<string> CreatePaymentSourceAsync(decimal amount, string description, string remarks)
        {
            // 1. Build Payload
            var request = new PayMongoSourceRequest
            {
                Data = new SourceData
                {
                    Attributes = new SourceAttributes
                    {
                        Amount = (int)(amount * 100), // Convert to cents
                        Type = "gcash",
                        Redirect = new RedirectUrls
                        {
                            Success = "https://localhost:7150/ar/receive-payment", // Frontend Redirect
                            Failed = "https://localhost:7150/ar/receive-payment"
                        },
                        Billing = new BillingInfo
                        {
                            Name = "System User",
                            Email = "user@example.com"
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 2. Call PayMongo API
            var response = await _httpClient.PostAsync("sources", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"PayMongo API Error: {responseString}");
            }

            // 3. Extract Checkout URL
            var result = JsonSerializer.Deserialize<PayMongoSourceResponse>(responseString);

            return result.Data.Attributes.Redirect.CheckoutUrl;
        }

        public bool VerifyWebhookSignature(string signature, string payload)
        {
            // In production, implement HMAC verification here
            return true;
        }
    }
}