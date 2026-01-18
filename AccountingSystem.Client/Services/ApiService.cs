using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly TokenStorageService _tokenService;

        public ApiService(HttpClient httpClient, TokenStorageService tokenService)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
        }

        private async Task AddAuthHeader()
        {
            var token = await _tokenService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<T> GetAsync<T>(string uri)
        {
            await AddAuthHeader();
            return await _httpClient.GetFromJsonAsync<T>(uri);
        }

        public async Task<HttpResponseMessage> PostAsync<T>(string uri, T data)
        {
            await AddAuthHeader();
            return await _httpClient.PostAsJsonAsync(uri, data);
        }

        public async Task<HttpResponseMessage> PutAsync<T>(string uri, T data)
        {
            await AddAuthHeader();
            return await _httpClient.PutAsJsonAsync(uri, data);
        }

        public async Task<HttpResponseMessage> DeleteAsync(string uri)
        {
            await AddAuthHeader();
            return await _httpClient.DeleteAsync(uri);
        }
    }
}