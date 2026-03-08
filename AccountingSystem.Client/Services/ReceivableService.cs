using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Enums;
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

        public async Task<List<CustomerDTO>?> GetCustomersAsync(bool includeArchived = false)
        {
            return await _api.GetAsync<List<CustomerDTO>>($"api/receivables/customers?includeArchived={includeArchived}");
        }

        public async Task RestoreCustomerAsync(int id)
        {
            var response = await _api.PutAsync<object?>($"api/receivables/customers/{id}/restore", null);
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        }

        public async Task<List<InvoiceDTO>?> GetInvoicesAsync(int? fiscalYear = null, DocumentStatus? status = null)
        {
            var queryParams = new List<string>();
            if (fiscalYear.HasValue)
                queryParams.Add($"fiscalYear={fiscalYear.Value}");
            if (status.HasValue)
                queryParams.Add($"status={status.Value}");

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
            return await _api.GetAsync<List<InvoiceDTO>>($"api/receivables/invoices{query}");
        }

        public async Task<DocumentCreationResultDTO?> CreateInvoiceAsync(CreateInvoiceDTO invoiceDto)
        {
            var response = await _api.PostAsync("api/receivables/invoice", invoiceDto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }

            return await response.Content.ReadFromJsonAsync<DocumentCreationResultDTO>();
        }

        public async Task<PaymentCreationResultDTO?> ReceivePaymentAsync(RecordPaymentDTO paymentDto)
        {
            var response = await _api.PostAsync($"api/receivables/invoice/{paymentDto.ReferenceId}/receive", paymentDto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }

            return await response.Content.ReadFromJsonAsync<PaymentCreationResultDTO>();
        }

        public async Task<CustomerDTO?> CreateCustomerAsync(CreateCustomerDTO customer)
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
    }
}
