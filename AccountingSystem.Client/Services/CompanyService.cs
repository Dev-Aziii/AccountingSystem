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

        public async Task<CompanyDTO?> GetCurrentCompanyAsync()
        {
            return await _api.GetAsync<CompanyDTO>("api/companies/current");
        }


        public async Task<List<DocumentSequenceDTO>?> GetDocumentSequencesAsync()
        {
            return await _api.GetAsync<List<DocumentSequenceDTO>>("api/document-numbering");
        }

        public async Task<DocumentSequenceDTO?> UpdateDocumentSequenceAsync(UpdateDocumentSequenceDTO dto)
        {
            var response = await _api.PutAsync("api/document-numbering", dto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }

            return await response.Content.ReadFromJsonAsync<DocumentSequenceDTO>();
        }

        public async Task UpdateCompanyAsync(UpdateCompanyDTO dto)
        {
            var response = await _api.PutAsync("api/companies/current", dto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }
    }
}