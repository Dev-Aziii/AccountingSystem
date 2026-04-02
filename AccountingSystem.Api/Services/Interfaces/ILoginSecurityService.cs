using AccountingSystem.API.Identity;
using AccountingSystem.API.Models;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface ILoginSecurityService
    {
        Task EnsureLoginAllowedAsync(User user, ApplicationUser? identityUser, string attemptedEmail);

        Task RecordInvalidPasswordAttemptAsync(User user, ApplicationUser? identityUser);

        Task ResetAfterSuccessfulLoginAsync(User user, ApplicationUser? identityUser);

        Task HandleAdminStatusChangeAsync(User user, string previousStatus, string newStatus);
    }
}
