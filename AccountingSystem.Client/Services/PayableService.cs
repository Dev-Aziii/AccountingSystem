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

        //Fetch list of bills
        public async Task<List<BillDTO>> GetBillsAsync()
        {
            return await _api.GetAsync<List<BillDTO>>("api/payables/bills");
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

        //  RecordPaymentDTO
        public async Task PayBillAsync(RecordPaymentDTO paymentDto)
        {
            var response = await _api.PostAsync($"api/payables/bill/{paymentDto.ReferenceId}/pay", paymentDto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        // CRUD for Vendors (kept from previous phases)
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
    }
}