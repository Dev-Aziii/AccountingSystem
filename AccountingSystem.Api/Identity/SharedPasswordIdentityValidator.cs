using AccountingSystem.Shared.Validation;
using Microsoft.AspNetCore.Identity;

namespace AccountingSystem.API.Identity
{
    public class SharedPasswordIdentityValidator : IPasswordValidator<ApplicationUser>
    {
        public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
        {
            if (!PasswordPolicy.TryValidate(password ?? string.Empty, out var validationMessage))
            {
                return Task.FromResult(IdentityResult.Failed(new IdentityError
                {
                    Code = "PasswordPolicy",
                    Description = validationMessage
                }));
            }

            return Task.FromResult(IdentityResult.Success);
        }
    }
}
