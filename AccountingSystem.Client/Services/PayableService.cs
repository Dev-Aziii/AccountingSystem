using AccountingSystem.Shared.DTOs;

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