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

        public async Task UpdateCompanyAsync(UpdateCompanyDTO dto)
        {
            var response = await _api.PutAsync("api/companies/current", dto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task<List<DocumentSequenceDTO>> GetDocumentSequencesAsync()
        {
            return await _api.GetAsync<List<DocumentSequenceDTO>>("api/companies/document-numbering") ?? new List<DocumentSequenceDTO>();
        }

        public async Task UpdateDocumentSequencesAsync(List<UpdateDocumentSequenceDTO> dto)
        {
            var response = await _api.PutAsync("api/companies/document-numbering", dto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }
    }
}