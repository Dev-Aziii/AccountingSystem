using AccountingSystem.API.Identity;
using AccountingSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.API.Services
{
    public class IdentityAccountService : IIdentityAccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public IdentityAccountService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task EnsureProvisionedAsync(
            LegacyIdentityUserSnapshot snapshot,
            string plainTextPassword,
            CancellationToken cancellationToken = default)
        {
            var user = await FindLinkedOrEmailMatchAsync(snapshot);
            if (user == null)
            {
                await EnsureRoleExistsAsync(snapshot.RoleName);

                user = CreateApplicationUser(snapshot);
                var createResult = await _userManager.CreateAsync(user, plainTextPassword);
                EnsureSucceeded(createResult, snapshot, "CreateAsync");

                await SyncRolesAsync(user, snapshot.RoleName);
                return;
            }

            ApplySnapshot(user, snapshot);
            await SyncPasswordHashAsync(user, plainTextPassword);
            await SyncRolesAsync(user, snapshot.RoleName);
        }

        public async Task EnsureProvisionedWithoutPasswordAsync(
            LegacyIdentityUserSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            var user = await FindLinkedOrEmailMatchAsync(snapshot);
            if (user == null)
            {
                await EnsureRoleExistsAsync(snapshot.RoleName);

                user = CreateApplicationUser(snapshot);
                var createResult = await _userManager.CreateAsync(user);
                EnsureSucceeded(createResult, snapshot, "CreateAsyncWithoutPassword");

                await SyncRolesAsync(user, snapshot.RoleName);
                return;
            }

            ApplySnapshot(user, snapshot);
            await PersistUserAsync(user, snapshot, "SyncExistingWithoutPassword");
            await SyncRolesAsync(user, snapshot.RoleName);
        }

        public async Task SyncExistingAsync(
            LegacyIdentityUserSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            var user = await FindLinkedOrEmailMatchAsync(snapshot);
            if (user == null)
            {
                return;
            }

            ApplySnapshot(user, snapshot);
            await PersistUserAsync(user, snapshot, "SyncExisting");
            await SyncRolesAsync(user, snapshot.RoleName);
        }

        public async Task SyncPasswordAsync(
            LegacyIdentityUserSnapshot snapshot,
            string plainTextPassword,
            bool createIfMissing,
            CancellationToken cancellationToken = default)
        {
            var user = await FindLinkedOrEmailMatchAsync(snapshot);
            if (user == null)
            {
                if (!createIfMissing)
                {
                    return;
                }

                await EnsureProvisionedAsync(snapshot, plainTextPassword, cancellationToken);
                return;
            }

            ApplySnapshot(user, snapshot);
            await SyncPasswordHashAsync(user, plainTextPassword);
            await SyncRolesAsync(user, snapshot.RoleName);
        }

        public async Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var normalizedEmail = _userManager.NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return null;
            }

            return await _userManager.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        }

        public async Task<ApplicationUser?> FindByLegacyUserIdAsync(int legacyUserId, CancellationToken cancellationToken = default)
        {
            return await _userManager.Users.FirstOrDefaultAsync(u => u.LegacyUserId == legacyUserId, cancellationToken);
        }

        private async Task<ApplicationUser?> FindLinkedOrEmailMatchAsync(LegacyIdentityUserSnapshot snapshot)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.LegacyUserId == snapshot.LegacyUserId);
            if (user != null)
            {
                return user;
            }

            var normalizedEmail = _userManager.NormalizeEmail(snapshot.Email);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return null;
            }

            user = await _userManager.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
            if (user == null)
            {
                return null;
            }

            if (user.LegacyUserId.HasValue && user.LegacyUserId.Value != snapshot.LegacyUserId)
            {
                throw new InvalidOperationException(
                    $"Identity user {user.Id} is already linked to legacy user {user.LegacyUserId.Value}.");
            }

            return user;
        }

        private static ApplicationUser CreateApplicationUser(LegacyIdentityUserSnapshot snapshot)
        {
            return new ApplicationUser
            {
                LegacyUserId = snapshot.LegacyUserId,
                CompanyId = snapshot.CompanyId,
                Email = snapshot.Email,
                UserName = snapshot.Email,
                FullName = snapshot.FullName,
                Status = snapshot.Status,
                IsActive = snapshot.IsActive,
                IsDeleted = snapshot.IsDeleted,
                RequireEmailConfirmation = snapshot.RequireEmailConfirmation ?? false,
                EmailConfirmed = snapshot.EmailConfirmed ?? !snapshot.RequireEmailConfirmation.GetValueOrDefault(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LockoutEnabled = true
            };
        }

        private void ApplySnapshot(ApplicationUser user, LegacyIdentityUserSnapshot snapshot)
        {
            user.LegacyUserId = snapshot.LegacyUserId;
            user.CompanyId = snapshot.CompanyId;
            user.Email = snapshot.Email;
            user.NormalizedEmail = _userManager.NormalizeEmail(snapshot.Email);
            user.UserName = snapshot.Email;
            user.NormalizedUserName = _userManager.NormalizeName(snapshot.Email);
            user.FullName = snapshot.FullName;
            user.Status = snapshot.Status;
            user.IsActive = snapshot.IsActive;
            user.IsDeleted = snapshot.IsDeleted;
            if (snapshot.RequireEmailConfirmation.HasValue)
            {
                user.RequireEmailConfirmation = snapshot.RequireEmailConfirmation.Value;
            }

            if (snapshot.EmailConfirmed.HasValue)
            {
                user.EmailConfirmed = snapshot.EmailConfirmed.Value;
            }

            user.UpdatedAt = DateTime.UtcNow;
            user.LockoutEnabled = true;
        }

        private async Task EnsureRoleExistsAsync(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                throw new InvalidOperationException($"Identity role '{roleName}' does not exist.");
            }
        }

        private async Task SyncRolesAsync(ApplicationUser user, string roleName)
        {
            await EnsureRoleExistsAsync(roleName);

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Count == 1 && string.Equals(currentRoles[0], roleName, StringComparison.Ordinal))
            {
                return;
            }

            if (currentRoles.Count > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                EnsureSucceeded(removeResult, user.LegacyUserId ?? 0, "RemoveFromRolesAsync");
            }

            var addResult = await _userManager.AddToRoleAsync(user, roleName);
            EnsureSucceeded(addResult, user.LegacyUserId ?? 0, "AddToRoleAsync");
        }

        private async Task SyncPasswordHashAsync(ApplicationUser user, string plainTextPassword)
        {
            var passwordMatches = !string.IsNullOrWhiteSpace(user.PasswordHash)
                && await _userManager.CheckPasswordAsync(user, plainTextPassword);

            if (passwordMatches)
            {
                await PersistUserAsync(user, user.LegacyUserId ?? 0, "SyncPasswordNoop");
                return;
            }

            user.PasswordHash = _userManager.PasswordHasher.HashPassword(user, plainTextPassword);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.UpdatedAt = DateTime.UtcNow;

            await PersistUserAsync(user, user.LegacyUserId ?? 0, "SyncPassword");
        }

        private async Task PersistUserAsync(ApplicationUser user, LegacyIdentityUserSnapshot snapshot, string operation)
        {
            var updateResult = await _userManager.UpdateAsync(user);
            EnsureSucceeded(updateResult, snapshot, operation);
        }

        private async Task PersistUserAsync(ApplicationUser user, int legacyUserId, string operation)
        {
            var updateResult = await _userManager.UpdateAsync(user);
            EnsureSucceeded(updateResult, legacyUserId, operation);
        }

        private static void EnsureSucceeded(IdentityResult result, LegacyIdentityUserSnapshot snapshot, string operation)
        {
            EnsureSucceeded(result, snapshot.LegacyUserId, operation);
        }

        private static void EnsureSucceeded(IdentityResult result, int legacyUserId, string operation)
        {
            if (result.Succeeded)
            {
                return;
            }

            var description = string.Join("; ", result.Errors.Select(e => $"{e.Code}:{e.Description}"));
            throw new InvalidOperationException(
                $"Identity operation '{operation}' failed for legacy user {legacyUserId}: {description}");
        }
    }
}
