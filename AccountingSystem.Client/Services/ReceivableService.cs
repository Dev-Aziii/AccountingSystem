using AccountingSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class ReceivableService
    {
        private readonly ApiService _api;

        public ReceivableService(ApiService api)
        {
            _api = api;
        }

        public async Task<List<CustomerDTO>> GetCustomersAsync()
        {
            return await _api.GetAsync<List<CustomerDTO>>("api/receivables/customers");
        }

        // --- NEW CRUD METHODS ---
        public async Task<CustomerDTO> CreateCustomerAsync(CreateCustomerDTO customer)
        {
            var response = await _api.PostAsync("api/receivables/customers", customer);
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());

            return await response.Content.ReadFromJsonAsync<CustomerDTO>();
        }

        public async Task UpdateCustomerAsync(UpdateCustomerDTO customer)
        {
            var response = await _api.PutAsync($"api/receivables/customers/{customer.Id}", customer);
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        }

        public async Task DeleteCustomerAsync(int id)
        {
            var response = await _api.DeleteAsync($"api/receivables/customers/{id}");
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        }
        // ------------------------

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