using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.Security;

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
            if (ApplicationAuthorizationScopeEvaluator.TryGetCompanyId(user, out var tenantId))
            {
                return tenantId;
            }

            return 0;
        }

        public void SetCurrentTenant(int tenantId)
        {
            _currentTenantId = tenantId;
        }
    }
}
