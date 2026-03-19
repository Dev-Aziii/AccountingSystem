namespace AccountingSystem.API.Services.Interfaces
{
    public interface IAuthSecurityAuditService
    {
        Task WriteAsync(
            string action,
            int? userId = null,
            int? companyId = null,
            string? email = null,
            string? reason = null,
            int? failedAttempts = null,
            DateTime? lockoutEndUtc = null,
            string? policy = null);
    }
}
