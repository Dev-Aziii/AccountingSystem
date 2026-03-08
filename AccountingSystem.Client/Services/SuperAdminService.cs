using AccountingSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class SuperAdminService
    {
        private readonly ApiService _api;

        public SuperAdminService(ApiService api)
        {
            _api = api;
        }

        //  Dashboard 
        public async Task<SystemDashboardDTO> GetDashboardStatsAsync()
        {
            return await _api.GetAsync<SystemDashboardDTO>("api/superadmin/dashboard")
                ?? throw new Exception("Failed to retrieve dashboard stats.");
        }

        // Tenant Management
        public async Task<List<TenantDTO>> GetAllCompaniesAsync()
        {
            return await _api.GetAsync<List<TenantDTO>>("api/superadmin/companies")
                ?? new List<TenantDTO>();
        }

        public async Task UpdateCompanyStatusAsync(int id, string status)
        {
            var response = await _api.PutAsync($"api/superadmin/companies/{id}/status", new UpdateCompanyStatusDTO { Status = status });
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task ToggleCompanyStatusAsync(int id)
        {
            var response = await _api.PutAsync<object?>($"api/superadmin/companies/{id}/toggle", null);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        // Global User Management 
        public async Task<List<GlobalUserDTO>> GetAllUsersAsync()
        {
            return await _api.GetAsync<List<GlobalUserDTO>>("api/superadmin/users")
                ?? new List<GlobalUserDTO>();
        }

        public async Task UpdateUserStatusAsync(int id, string status)
        {
            var response = await _api.PutAsync($"api/superadmin/users/{id}/status", new UpdateUserStatusDTO { Status = status });
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task ToggleUserStatusAsync(int id)
        {
            var response = await _api.PutAsync<object?>($"api/superadmin/users/{id}/toggle", null);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        // Super Admin Audit Logs 
        public async Task<List<SuperAdminAuditLogDTO>> GetAuditLogsAsync()
        {
            return await _api.GetAsync<List<SuperAdminAuditLogDTO>>("api/superadmin/audit-logs")
                ?? new List<SuperAdminAuditLogDTO>();
        }
    }
}