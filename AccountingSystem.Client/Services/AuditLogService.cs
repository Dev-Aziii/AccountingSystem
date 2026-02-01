using AccountingSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class AuditLogService
    {
        private readonly ApiService _api;

        public AuditLogService(ApiService api)
        {
            _api = api;
        }

        public async Task<List<AuditLogDTO>?> GetAuditLogsAsync()
        {
            return await _api.GetAsync<List<AuditLogDTO>>("api/audit-logs");
        }
    }
}