using AccountingSystem.API.Configuration;
using AccountingSystem.API.Identity;
using AccountingSystem.API.Models;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.Validation;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Data
{
    public static class DataSeeder
    {
        public static async Task SeedDataAsync(
            AccountingDbContext context,
            IdentityAuthDbContext identityContext,
            IIdentityAccountService identityAccountService,
            IConfiguration configuration)
        {
            var hostCompany = await context.Companies
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Name == "SaaS Operations");

            if (hostCompany == null)
            {
                hostCompany = new Company
                {
                    Name = "SaaS Operations",
                    Address = "HQ",
                    TaxId = "000",
                    Currency = "PHP",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Status = "Active",
                    FiscalYearStartMonth = 1
                };

                context.Companies.Add(hostCompany);
                await context.SaveChangesAsync();
            }

            var superAdminRole = await context.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Name == "SuperAdmin")
                ?? throw new InvalidOperationException("Legacy role 'SuperAdmin' is missing.");

            var legacySuperAdmin = await context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.RoleId == superAdminRole.Id && !u.IsDeleted);

            if (legacySuperAdmin != null)
            {
                var linkedIdentityUser = await identityContext.Users.FirstOrDefaultAsync(u => u.LegacyUserId == legacySuperAdmin.Id);
                if (linkedIdentityUser == null && !StartupConfigurationValidator.IsMissingOrPlaceholder(configuration["BootstrapAdmin:InitialPassword"]))
                {
                    await identityAccountService.EnsureProvisionedAsync(
                        CreateIdentitySnapshot(legacySuperAdmin, superAdminRole.Name, requireEmailConfirmation: false, emailConfirmed: true),
                        configuration["BootstrapAdmin:InitialPassword"]!);
                }

                return;
            }

            var bootstrapEmail = configuration["BootstrapAdmin:Email"];
            var bootstrapFullName = configuration["BootstrapAdmin:FullName"];
            var bootstrapPassword = configuration["BootstrapAdmin:InitialPassword"];

            if (StartupConfigurationValidator.IsMissingOrPlaceholder(bootstrapEmail) ||
                StartupConfigurationValidator.IsMissingOrPlaceholder(bootstrapFullName) ||
                StartupConfigurationValidator.IsMissingOrPlaceholder(bootstrapPassword))
            {
                throw new InvalidOperationException(
                    "Bootstrap admin configuration is required to initialize the first super-admin. " +
                    "Provide BootstrapAdmin:Email, BootstrapAdmin:FullName, and BootstrapAdmin:InitialPassword via user-secrets or environment variables.");
            }

            if (!PasswordPolicy.TryValidate(bootstrapPassword!, out var passwordValidationMessage))
            {
                throw new InvalidOperationException(
                    $"BootstrapAdmin:InitialPassword does not satisfy the current password policy. {passwordValidationMessage}");
            }

            legacySuperAdmin = new User
            {
                CompanyId = hostCompany.Id,
                Email = bootstrapEmail!,
                FullName = bootstrapFullName!,
                RoleId = superAdminRole.Id,
                Role = superAdminRole,
                PasswordHash = string.Empty,
                PasswordSalt = null,
                IsActive = true,
                Status = "Active"
            };

            context.Users.Add(legacySuperAdmin);
            await context.SaveChangesAsync();

            await identityAccountService.EnsureProvisionedAsync(
                CreateIdentitySnapshot(legacySuperAdmin, superAdminRole.Name, requireEmailConfirmation: false, emailConfirmed: true),
                bootstrapPassword!);
        }

        private static LegacyIdentityUserSnapshot CreateIdentitySnapshot(
            User user,
            string roleName,
            bool? requireEmailConfirmation = null,
            bool? emailConfirmed = null) =>
            new(
                user.Id,
                user.CompanyId,
                user.Email,
                user.FullName ?? user.Email,
                user.Status,
                user.IsActive,
                user.IsDeleted,
                roleName,
                requireEmailConfirmation,
                emailConfirmed);
    }
}
