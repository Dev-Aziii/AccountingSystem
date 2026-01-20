using AccountingSystem.Client.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using System.Net.Http.Json;
using System.Text.Json;

namespace AccountingSystem.Client.Services
{
    public class PaymentClientService : IPaymentClientService
    {
        private readonly HttpClient _http;
        private readonly ApiService _api; // Use ApiService for consistent auth headers

        public PaymentClientService(HttpClient http, ApiService api)
        {
            _http = http;
            _api = api;
        }

        public async Task<string> CreatePaymentLinkAsync(CreateSourceDTO sourceDto)
        {
            // We use PostAsync from ApiService to ensure Auth headers are present
            var response = await _api.PostAsync("api/payments/paymongo-source", sourceDto);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Payment initialization failed: {error}");
            }

            // Deserialize the new DTO
            var result = await response.Content.ReadFromJsonAsync<PaymentSourceResponseDTO>();

            // IMPORTANT: Return both ID and URL (we will need to change interface return type or handle storage here)
            // Ideally, we return the object. For now, let's store the ID in LocalStorage via the Component to keep service simple
            // We'll hack the return to just be the URL for now, but we need that ID.

            return result.CheckoutUrl;
        }

        // NEW Method to get full object
        public async Task<PaymentSourceResponseDTO> CreatePaymentSourceFullAsync(CreateSourceDTO sourceDto)
        {
            var response = await _api.PostAsync("api/payments/paymongo-source", sourceDto);
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
            return await response.Content.ReadFromJsonAsync<PaymentSourceResponseDTO>();
        }
    }
}