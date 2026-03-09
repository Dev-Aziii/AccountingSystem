using Microsoft.JSInterop;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly TokenStorageService _tokenService;
        private readonly IJSRuntime _js;

        public ApiService(HttpClient httpClient, TokenStorageService tokenService, IJSRuntime js)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
            _js = js;
        }

        private async Task PrepareAuthHeaderAsync(bool requiresAuth)
        {
            if (!requiresAuth)
            {
                ClearAuthHeader();
                return;
            }

            var token = await _tokenService.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                ClearAuthHeader();
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public void ClearAuthHeader()
        {
            if (_httpClient.DefaultRequestHeaders.Authorization != null)
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }

        public async Task<T?> GetAsync<T>(string uri, bool requiresAuth = true)
        {
            await PrepareAuthHeaderAsync(requiresAuth);
            return await _httpClient.GetFromJsonAsync<T>(uri);
        }

        public async Task<HttpResponseMessage> PostAsync<T>(string uri, T data, bool requiresAuth = true)
        {
            await PrepareAuthHeaderAsync(requiresAuth);
            return await _httpClient.PostAsJsonAsync(uri, data);
        }

        public async Task<HttpResponseMessage> PutAsync<T>(string uri, T data, bool requiresAuth = true)
        {
            await PrepareAuthHeaderAsync(requiresAuth);
            return await _httpClient.PutAsJsonAsync(uri, data);
        }

        public async Task<HttpResponseMessage> DeleteAsync(string uri, bool requiresAuth = true)
        {
            await PrepareAuthHeaderAsync(requiresAuth);
            return await _httpClient.DeleteAsync(uri);
        }

        public async Task DownloadFileAsync(string uri, string fileName, bool requiresAuth = true)
        {
            await PrepareAuthHeaderAsync(requiresAuth);
            var response = await _httpClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }

            var fileStream = await response.Content.ReadAsStreamAsync();
            using var streamRef = new DotNetStreamReference(fileStream);
            await _js.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
        }
    }
}
