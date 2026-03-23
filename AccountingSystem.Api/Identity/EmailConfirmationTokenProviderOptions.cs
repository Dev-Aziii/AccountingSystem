using Microsoft.AspNetCore.Identity;

namespace AccountingSystem.API.Identity
{
    public sealed class EmailConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions
    {
        public EmailConfirmationTokenProviderOptions()
        {
            Name = IdentityTokenProviderNames.EmailConfirmation;
        }
    }
}
