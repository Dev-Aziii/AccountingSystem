using AccountingSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class CompanyService
    {
        private readonly ApiService _api;

        public CompanyService(ApiService api)
        {
            _api = api;
        }

        public async Task<CompanyDTO> GetCurrentCompanyAsync()
        {
            return await _api.GetAsync<CompanyDTO>("api/companies/current");
        }
    }
}