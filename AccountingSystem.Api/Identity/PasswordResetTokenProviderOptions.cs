using Microsoft.AspNetCore.Identity;

namespace AccountingSystem.API.Identity
{
    public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
    {
        public PasswordResetTokenProviderOptions()
        {
            Name = IdentityTokenProviderNames.PasswordReset;
        }
    }
}
