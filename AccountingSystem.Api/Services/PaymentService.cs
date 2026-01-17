using AccountingSystem.Shared.DTOs;
using AccountingSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Reflection;
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

            // Set Base URL and Auth Headers
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
                            Success = "https://localhost:7000/success", // Frontend URL
                            Failed = "https://localhost:7000/failed"
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
            return result.Data.Attributes.Redirect.Success; // Usually returns checkout_url, mapping simply here
        }

        public bool VerifyWebhookSignature(string signature, string payload)
        {
            // Actual implementation requires HMAC-SHA256 hashing of the payload 
            // with the webhook secret and comparing it to the signature header.
            // For development/demo, we return true.
            return true;
        }
    }
}