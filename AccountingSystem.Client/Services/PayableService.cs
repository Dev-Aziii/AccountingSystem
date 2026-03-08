using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Enums;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class PayableService
    {
        private readonly ApiService _api;

        public PayableService(ApiService api)
        {
            _api = api;
        }

        public async Task<List<VendorDTO>?> GetVendorsAsync(bool includeArchived = false)
        {
            return await _api.GetAsync<List<VendorDTO>>($"api/payables/vendors?includeArchived={includeArchived}");
        }

        public async Task RestoreVendorAsync(int id)
        {
            var response = await _api.PutAsync<object?>($"api/payables/vendors/{id}/restore", null);
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        }

        public async Task<List<BillDTO>?> GetBillsAsync(int? fiscalYear = null, DocumentStatus? status = null)
        {
            var queryParams = new List<string>();
            if (fiscalYear.HasValue)
                queryParams.Add($"fiscalYear={fiscalYear.Value}");
            if (status.HasValue)
                queryParams.Add($"status={status.Value}");

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
            return await _api.GetAsync<List<BillDTO>>($"api/payables/bills{query}");
        }

        public async Task CreateBillAsync(CreateBillDTO billDto)
        {
            var response = await _api.PostAsync("api/payables/bill", billDto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task<PaymentCreationResultDTO?> PayBillAsync(RecordPaymentDTO paymentDto)
        {
            var response = await _api.PostAsync($"api/payables/bill/{paymentDto.ReferenceId}/pay", paymentDto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }

            return await response.Content.ReadFromJsonAsync<PaymentCreationResultDTO>();
        }

        public async Task<VendorDTO?> CreateVendorAsync(CreateVendorDTO vendor)
        {
            var response = await _api.PostAsync("api/payables/vendors", vendor);
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
            return await response.Content.ReadFromJsonAsync<VendorDTO>();
        }

        public async Task UpdateVendorAsync(UpdateVendorDTO vendor)
        {
            var response = await _api.PutAsync($"api/payables/vendors/{vendor.Id}", vendor);
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        }

        public async Task DeleteVendorAsync(int id)
        {
            var response = await _api.DeleteAsync($"api/payables/vendors/{id}");
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        }
    }
}
