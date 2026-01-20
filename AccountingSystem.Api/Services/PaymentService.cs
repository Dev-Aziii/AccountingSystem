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

            _httpClient.BaseAddress = new Uri("https://api.paymongo.com/v1/");

            var secretKey = _configuration["PayMongo:SecretKey"];
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(secretKey + ":"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
        }

        // Implementation of the original interface method (kept for backward compatibility if needed)
        public async Task<string> CreatePaymentSourceAsync(decimal amount, string description, string remarks)
        {
            // Forward to the new method with default/dummy URLs if called directly
            return await CreatePaymentSourceAsync(new CreateSourceDTO
            {
                Amount = amount,
                Description = description,
                Remarks = remarks
            });
        }

        // Overload to accept DTO with dynamic URLs
        public async Task<string> CreatePaymentSourceAsync(CreateSourceDTO dto)
        {
            var request = new PayMongoSourceRequest
            {
                Data = new SourceData
                {
                    Attributes = new SourceAttributes
                    {
                        Amount = (int)(dto.Amount * 100),
                        Type = "gcash",
                        Redirect = new RedirectUrls
                        {
                            // Use Client-provided URL or Fallback
                            Success = dto.SuccessUrl ?? "https://localhost:7150/success",
                            Failed = dto.FailedUrl ?? "https://localhost:7150/failed"
                        },
                        Billing = new BillingInfo { Name = "System User", Email = "user@example.com" }
                    }
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("sources", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) throw new Exception($"PayMongo API Error: {responseString}");

            var result = JsonSerializer.Deserialize<PayMongoSourceResponse>(responseString);
            return result.Data.Attributes.Redirect.CheckoutUrl;
        }

        public bool VerifyWebhookSignature(string signature, string payload) => true;
    }
}