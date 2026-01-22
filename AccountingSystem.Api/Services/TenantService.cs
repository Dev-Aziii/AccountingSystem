using AccountingSystem.API.Services.Interfaces;
using System.Security.Claims;

namespace AccountingSystem.API.Services
{
    public class TenantService : ITenantService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private int? _currentTenantId;

        public TenantService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int GetCurrentTenant()
        {
            if (_currentTenantId.HasValue)
            {
                return _currentTenantId.Value;
            }

            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return 0;

            // Retrieve CompanyId from JWT Claims
            var tenantClaim = user.Claims.FirstOrDefault(c => c.Type == "CompanyId");
            if (tenantClaim != null && int.TryParse(tenantClaim.Value, out int tenantId))
            {
                return tenantId;
            }

            return 0; // No tenant found (or System Admin context)
        }

        public void SetCurrentTenant(int tenantId)
        {
            _currentTenantId = tenantId;
        }
    }
}