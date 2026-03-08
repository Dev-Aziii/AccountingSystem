using AccountingSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class UserService
    {
        private readonly ApiService _api;

        public UserService(ApiService api)
        {
            _api = api;
        }

        public async Task<List<UserDTO>?> GetAllUsersAsync(bool includeArchived = false)
        {
            return await _api.GetAsync<List<UserDTO>>($"api/users?includeArchived={includeArchived}");
        }

        public async Task RestoreUserAsync(int id)
        {
            var response = await _api.PutAsync<object?>($"api/users/{id}/restore", null);
            if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        }

        public async Task CreateUserAsync(RegisterDTO registerDto)
        {
            var response = await _api.PostAsync("api/users", registerDto);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task DeleteUserAsync(int id)
        {
            var response = await _api.DeleteAsync($"api/users/{id}");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }
    }
}