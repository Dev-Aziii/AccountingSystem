using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.Client.Services
{
    public class ReceivableService
    {
        private readonly ApiService _api;

        public ReceivableService(ApiService api)
        {
            _api = api;
        }

        public async Task CreateInvoiceAsync(CreateInvoiceDTO invoiceDto)
        {
            var response = await _api.PostAsync("api/receivables/invoice", invoiceDto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task ReceivePaymentAsync(int invoiceId, ProcessPaymentDTO paymentDto)
        {
            var response = await _api.PostAsync($"api/receivables/invoice/{invoiceId}/receive", paymentDto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }
    }
}