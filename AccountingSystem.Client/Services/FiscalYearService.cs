using AccountingSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class FiscalYearService
    {
        private readonly ApiService _api;
        public FiscalYearService(ApiService api) { _api = api; }

        public Task<List<FiscalYearDTO>?> GetAllAsync() => _api.GetAsync<List<FiscalYearDTO>>("api/fiscal-years");

        public async Task CreateAsync(CreateFiscalYearDTO dto)
        {
            var res = await _api.PostAsync("api/fiscal-years", dto);
            if (!res.IsSuccessStatusCode) throw new Exception(await res.Content.ReadAsStringAsync());
        }

        public async Task CloseAsync(int id)
        {
            var res = await _api.PostAsync($"api/fiscal-years/{id}/close", new { });
            if (!res.IsSuccessStatusCode) throw new Exception(await res.Content.ReadAsStringAsync());
        }
    }
}
