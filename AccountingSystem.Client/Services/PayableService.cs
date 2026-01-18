using AccountingSystem.Shared.DTOs;
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

        public async Task<List<VendorDTO>> GetVendorsAsync()
        {
            return await _api.GetAsync<List<VendorDTO>>("api/payables/vendors");
        }

        // --- NEW CRUD METHODS ---
        public async Task<VendorDTO> CreateVendorAsync(CreateVendorDTO vendor)
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
        // ------------------------

        public async Task CreateBillAsync(CreateBillDTO billDto)
        {
            var response = await _api.PostAsync("api/payables/bill", billDto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task PayBillAsync(int billId, ProcessPaymentDTO paymentDto)
        {
            var response = await _api.PostAsync($"api/payables/bill/{billId}/pay", paymentDto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }
    }
}