using AccountingSystem.Shared.DTOs;

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
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to restore user. Please try again."));
            }
        }

        public async Task CreateUserAsync(RegisterDTO registerDto)
        {
            var response = await _api.PostAsync("api/users", registerDto);
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to create user. Please try again."));
            }
        }

        public async Task DeleteUserAsync(int id)
        {
            var response = await _api.DeleteAsync($"api/users/{id}");
            if (!response.IsSuccessStatusCode)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                throw new Exception(ApiErrorParser.Extract(rawContent, "Unable to archive user. Please try again."));
            }
        }
    }
}
