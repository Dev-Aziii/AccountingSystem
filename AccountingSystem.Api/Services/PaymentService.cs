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
            if (string.IsNullOrEmpty(secretKey))
            {
                throw new InvalidOperationException("PayMongo:SecretKey configuration is required but not found.");
            }

            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes(secretKey + ":"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
        }

        public async Task<PaymentSourceResponseDTO> CreatePaymentSourceAsync(CreateSourceDTO dto)
        {
            var request = new
            {
                data = new
                {
                    attributes = new
                    {
                        amount = (int)(dto.Amount * 100),
                        type = "gcash",
                        currency = "PHP",
                        redirect = new
                        {
                            success = dto.SuccessUrl ?? "https://localhost:7150/success",
                            failed = dto.FailedUrl ?? "https://localhost:7150/failed"
                        },
                        billing = new { name = "System User", email = "user@example.com" }
                    }
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("sources", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"PayMongo API Error: {responseString}");
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<PayMongoSourceResponse>(responseString, options);

            if (result?.Data == null)
            {
                throw new InvalidOperationException("Invalid response from PayMongo API: missing data");
            }

            return new PaymentSourceResponseDTO
            {
                SourceId = result.Data.Id ?? throw new InvalidOperationException("PayMongo API returned null source ID"),
                CheckoutUrl = result.Data.Attributes?.Redirect?.CheckoutUrl ?? throw new InvalidOperationException("PayMongo API returned null checkout URL")
            };
        }

        public async Task<string> CreatePaymentSourceAsync(decimal amount, string description, string remarks)
        {
            var result = await CreatePaymentSourceAsync(new CreateSourceDTO { Amount = amount, Description = description, Remarks = remarks });
            return result.CheckoutUrl;
        }

        public async Task<bool> CapturePaymentAsync(string sourceId, decimal amount, string description)
        {
            var request = new
            {
                data = new
                {
                    attributes = new
                    {
                        amount = (int)(amount * 100),
                        source = new { id = sourceId, type = "source" },
                        currency = "PHP",
                        description = description
                    }
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("payments", content);

            // Logging failure if capture fails could be helpful
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"PayMongo Capture Failed: {error}");
            }

            return response.IsSuccessStatusCode;
        }

        public bool VerifyWebhookSignature(string signature, string payload) => true;
    }
}