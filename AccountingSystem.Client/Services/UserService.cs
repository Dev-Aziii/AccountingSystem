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

        public async Task<List<UserDTO>> GetAllUsersAsync()
        {
            return await _api.GetAsync<List<UserDTO>>("api/users");
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