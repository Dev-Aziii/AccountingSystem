namespace AccountingSystem.API.Services.Interfaces
{
    public interface ITenantService
    {
        int GetCurrentTenant();
        void SetCurrentTenant(int tenantId); 
    }
}