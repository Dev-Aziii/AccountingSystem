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
            var response = await _api.PostAsync("api/payments/paymongo-source", sourceDto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Payment initialization failed: {error}");
            }

            // Deserialize the  DTO
            var result = await response.Content.ReadFromJsonAsync<PaymentSourceResponseDTO>();

            if (result == null)
            {
                throw new Exception("Failed to deserialize payment response");
            }
            return result.CheckoutUrl;
        }

        public async Task<PaymentSourceResponseDTO> CreatePaymentSourceFullAsync(CreateSourceDTO sourceDto)
        {
            var response = await _api.PostAsync("api/payments/paymongo-source", sourceDto);
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());

            var result = await response.Content.ReadFromJsonAsync<PaymentSourceResponseDTO>();

            if (result == null)
            {
                throw new Exception("Failed to deserialize payment response");
            }

            return result;
        }
    }
}