using AccountingSystem.Shared.DTOs;
using System.Net.Http.Json;
using System.Text.Json;

namespace AccountingSystem.Client.Services
{
    public class PaymentClientService : IPaymentClientService
    {
        private readonly HttpClient _http;

        public PaymentClientService(HttpClient http)
        {
            _http = http;
        }

        public async Task<string> CreatePaymentLinkAsync(CreateSourceDTO sourceDto)
        {
            var response = await _http.PostAsJsonAsync("api/payments/paymongo-source", sourceDto);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Payment initialization failed: {error}");
            }

            // The API returns an anonymous object { checkoutUrl = "..." }
            // We parse it manually or use a specific DTO if preferred.
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (result.TryGetProperty("checkoutUrl", out var urlProperty))
            {
                return urlProperty.GetString();
            }

            throw new Exception("Invalid response from payment server.");
        }
    }
}