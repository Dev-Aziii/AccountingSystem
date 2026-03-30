using AccountingSystem.API.Configuration;
using AccountingSystem.API.Identity;
using AccountingSystem.API.Security;
using AccountingSystem.API.Services.Interfaces;
using AccountingSystem.Shared.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AccountingSystem.API.Services
{
    public class MfaService : IMfaService
    {
        private const int RecoveryCodeCount = 10;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IIdentityAccountService _identityAccountService;
        private readonly ILoginChallengeTokenService _loginChallengeTokenService;
        private readonly IAuthSecurityAuditService _auditService;
        private readonly string _issuer;

        public MfaService(
            UserManager<ApplicationUser> userManager,
            IIdentityAccountService identityAccountService,
            ILoginChallengeTokenService loginChallengeTokenService,
            IAuthSecurityAuditService auditService,
            IOptions<MfaSettings> settings)
        {
            _userManager = userManager;
            _identityAccountService = identityAccountService;
            _loginChallengeTokenService = loginChallengeTokenService;
            _auditService = auditService;
            _issuer = string.IsNullOrWhiteSpace(settings.Value.AuthenticatorIssuer)
                ? "AccountingSystem"
                : settings.Value.AuthenticatorIssuer.Trim();
        }

        public async Task<MfaStatusDTO> GetStatusAsync(int legacyUserId)
        {
            var identityUser = await RequireIdentityUserAsync(legacyUserId);
            var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(identityUser);
            var isTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(identityUser);

            return new MfaStatusDTO
            {
                IsTwoFactorEnabled = isTwoFactorEnabled,
                HasAuthenticatorKey = !string.IsNullOrWhiteSpace(authenticatorKey),
                RecoveryCodesLeft = isTwoFactorEnabled
                    ? await _userManager.CountRecoveryCodesAsync(identityUser)
                    : 0
            };
        }

        public async Task<MfaSetupDTO> BeginAuthenticatorSetupAsync(int legacyUserId)
        {
            var identityUser = await RequireIdentityUserAsync(legacyUserId);
            var setup = await BuildSetupDtoAsync(identityUser);

            await _auditService.WriteAsync(
                "AUTH-MFA-SETUP-STARTED",
                userId: identityUser.LegacyUserId,
                companyId: identityUser.CompanyId,
                email: identityUser.Email,
                reason: setup.IsTwoFactorEnabled ? "AlreadyEnabled" : "PendingVerification");

            return setup;
        }

        public async Task<MfaSetupDTO> ResetAuthenticatorAsync(int legacyUserId, MfaReauthenticationDTO dto)
        {
            var identityUser = await RequireIdentityUserAsync(legacyUserId);
            await ReauthenticateAsync(identityUser, dto);

            identityUser.UpdatedAt = DateTime.UtcNow;
            EnsureIdentitySucceeded(await _userManager.SetTwoFactorEnabledAsync(identityUser, false), "DisableTwoFactorForReset");

            identityUser = await RequireIdentityUserByIdAsync(identityUser.Id);
            identityUser.UpdatedAt = DateTime.UtcNow;
            EnsureIdentitySucceeded(await _userManager.ResetAuthenticatorKeyAsync(identityUser), "ResetAuthenticatorKey");

            var refreshedUser = await RequireIdentityUserByIdAsync(identityUser.Id);
            var setup = await BuildSetupDtoAsync(refreshedUser);

            await _auditService.WriteAsync(
                "AUTH-MFA-RESET",
                userId: refreshedUser.LegacyUserId,
                companyId: refreshedUser.CompanyId,
                email: refreshedUser.Email,
                reason: "AuthenticatorReset");

            return setup;
        }

        public async Task<RecoveryCodesDTO> VerifyAuthenticatorSetupAsync(int legacyUserId, VerifyAuthenticatorSetupDTO dto)
        {
            var identityUser = await RequireIdentityUserAsync(legacyUserId);
            await EnsureAuthenticatorKeyAsync(identityUser);

            var sanitizedCode = NormalizeAuthenticatorCode(dto.Code);
            if (!IsValidAuthenticatorCode(sanitizedCode) ||
                !await _userManager.VerifyTwoFactorTokenAsync(
                    identityUser,
                    _userManager.Options.Tokens.AuthenticatorTokenProvider,
                    sanitizedCode))
            {
                await _auditService.WriteAsync(
                    "AUTH-MFA-ENABLE-FAILURE",
                    userId: identityUser.LegacyUserId,
                    companyId: identityUser.CompanyId,
                    email: identityUser.Email,
                    reason: "InvalidAuthenticatorCode");
                throw new InvalidOperationException("The verification code is invalid. Please try again.");
            }

            identityUser.UpdatedAt = DateTime.UtcNow;
            EnsureIdentitySucceeded(await _userManager.SetTwoFactorEnabledAsync(identityUser, true), "EnableTwoFactor");

            var refreshedUser = await RequireIdentityUserByIdAsync(identityUser.Id);
            var recoveryCodes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(refreshedUser, RecoveryCodeCount)
                ?? Enumerable.Empty<string>())
                .ToList();

            await _auditService.WriteAsync(
                "AUTH-MFA-ENABLED",
                userId: refreshedUser.LegacyUserId,
                companyId: refreshedUser.CompanyId,
                email: refreshedUser.Email,
                reason: "AuthenticatorVerified");

            return new RecoveryCodesDTO
            {
                RecoveryCodes = recoveryCodes
            };
        }

        public async Task<RecoveryCodesDTO> RegenerateRecoveryCodesAsync(int legacyUserId, MfaReauthenticationDTO dto)
        {
            var identityUser = await RequireIdentityUserAsync(legacyUserId);
            await ReauthenticateAsync(identityUser, dto);

            if (!await _userManager.GetTwoFactorEnabledAsync(identityUser))
            {
                throw new InvalidOperationException("Two-factor authentication is not enabled for this account.");
            }

            var refreshedUser = await RequireIdentityUserByIdAsync(identityUser.Id);
            var recoveryCodes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(refreshedUser, RecoveryCodeCount)
                ?? Enumerable.Empty<string>())
                .ToList();

            await _auditService.WriteAsync(
                "AUTH-MFA-RECOVERY-CODES-REGENERATED",
                userId: refreshedUser.LegacyUserId,
                companyId: refreshedUser.CompanyId,
                email: refreshedUser.Email,
                reason: "RecoveryCodesRegenerated");

            return new RecoveryCodesDTO
            {
                RecoveryCodes = recoveryCodes
            };
        }

        public async Task DisableAsync(int legacyUserId, MfaReauthenticationDTO dto)
        {
            var identityUser = await RequireIdentityUserAsync(legacyUserId);
            await ReauthenticateAsync(identityUser, dto);

            identityUser.UpdatedAt = DateTime.UtcNow;
            EnsureIdentitySucceeded(await _userManager.SetTwoFactorEnabledAsync(identityUser, false), "DisableTwoFactor");

            identityUser = await RequireIdentityUserByIdAsync(identityUser.Id);
            identityUser.UpdatedAt = DateTime.UtcNow;
            EnsureIdentitySucceeded(await _userManager.ResetAuthenticatorKeyAsync(identityUser), "ResetAuthenticatorKeyOnDisable");

            await _auditService.WriteAsync(
                "AUTH-MFA-DISABLED",
                userId: identityUser.LegacyUserId,
                companyId: identityUser.CompanyId,
                email: identityUser.Email,
                reason: "AuthenticatorDisabled");
        }

        public async Task<MfaLoginVerificationResult> VerifyLoginChallengeAsync(LoginMfaDTO dto)
        {
            var challenge = _loginChallengeTokenService.Validate(dto.ChallengeToken);
            var identityUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == challenge.IdentityUserId);
            if (identityUser == null)
            {
                await _auditService.WriteAsync(
                    "AUTH-MFA-LOGIN-FAILURE",
                    userId: challenge.LegacyUserId,
                    reason: "ChallengeIdentityUserNotFound");
                throw new AuthFailureException(
                    "MfaChallengeInvalid",
                    "The sign-in verification session is invalid or expired. Please sign in again.");
            }

            if (identityUser.LegacyUserId != challenge.LegacyUserId)
            {
                await _auditService.WriteAsync(
                    "AUTH-MFA-LOGIN-FAILURE",
                    userId: identityUser.LegacyUserId,
                    companyId: identityUser.CompanyId,
                    email: identityUser.Email,
                    reason: "ChallengeLegacyUserMismatch");
                throw new AuthFailureException(
                    "MfaChallengeInvalid",
                    "The sign-in verification session is invalid or expired. Please sign in again.");
            }

            if (!await _userManager.GetTwoFactorEnabledAsync(identityUser))
            {
                await _auditService.WriteAsync(
                    "AUTH-MFA-LOGIN-FAILURE",
                    userId: identityUser.LegacyUserId,
                    companyId: identityUser.CompanyId,
                    email: identityUser.Email,
                    reason: "ChallengeTwoFactorDisabled");
                throw new AuthFailureException(
                    "MfaChallengeInvalid",
                    "The sign-in verification session is invalid or expired. Please sign in again.");
            }

            var hasAuthenticatorCode = !string.IsNullOrWhiteSpace(dto.TwoFactorCode);
            var hasRecoveryCode = !string.IsNullOrWhiteSpace(dto.RecoveryCode);
            if (hasAuthenticatorCode == hasRecoveryCode)
            {
                throw new AuthFailureException(
                    "MfaFactorSelectionInvalid",
                    "Enter either a 6-digit authenticator code or a recovery code.",
                    StatusCodes.Status400BadRequest);
            }

            if (hasAuthenticatorCode)
            {
                var sanitizedCode = NormalizeAuthenticatorCode(dto.TwoFactorCode);
                if (!IsValidAuthenticatorCode(sanitizedCode) ||
                    !await _userManager.VerifyTwoFactorTokenAsync(
                        identityUser,
                        _userManager.Options.Tokens.AuthenticatorTokenProvider,
                        sanitizedCode))
                {
                    await _auditService.WriteAsync(
                        "AUTH-MFA-LOGIN-FAILURE",
                        userId: identityUser.LegacyUserId,
                        companyId: identityUser.CompanyId,
                        email: identityUser.Email,
                        reason: "InvalidAuthenticatorCode");
                    throw new AuthFailureException(
                        "InvalidTwoFactorCode",
                        "The verification code is invalid. Please try again.");
                }

                return new MfaLoginVerificationResult(identityUser, false);
            }

            var recoveryResult = await RedeemRecoveryCodeAsync(identityUser, dto.RecoveryCode);
            if (!recoveryResult.Succeeded)
            {
                await _auditService.WriteAsync(
                    "AUTH-MFA-RECOVERY-CODE-FAILURE",
                    userId: identityUser.LegacyUserId,
                    companyId: identityUser.CompanyId,
                    email: identityUser.Email,
                    reason: "InvalidRecoveryCode");
                throw new AuthFailureException(
                    "InvalidRecoveryCode",
                    "The recovery code is invalid. Please try again.");
            }

            return new MfaLoginVerificationResult(identityUser, true);
        }

        private async Task ReauthenticateAsync(ApplicationUser identityUser, MfaReauthenticationDTO dto)
        {
            var methodsUsed = CountProvidedMethods(dto);
            if (methodsUsed != 1)
            {
                throw new InvalidOperationException("Provide exactly one verification method: current password, authenticator code, or recovery code.");
            }

            if (!string.IsNullOrWhiteSpace(dto.CurrentPassword))
            {
                if (!await _userManager.CheckPasswordAsync(identityUser, dto.CurrentPassword))
                {
                    await _auditService.WriteAsync(
                        "AUTH-MFA-REAUTH-FAILURE",
                        userId: identityUser.LegacyUserId,
                        companyId: identityUser.CompanyId,
                        email: identityUser.Email,
                        reason: "InvalidCurrentPassword");
                    throw new InvalidOperationException("The current password is incorrect.");
                }

                return;
            }

            if (!await _userManager.GetTwoFactorEnabledAsync(identityUser))
            {
                throw new InvalidOperationException("Two-factor authentication is not enabled for this account. Use your current password to continue.");
            }

            if (!string.IsNullOrWhiteSpace(dto.TwoFactorCode))
            {
                var sanitizedCode = NormalizeAuthenticatorCode(dto.TwoFactorCode);
                if (!IsValidAuthenticatorCode(sanitizedCode) ||
                    !await _userManager.VerifyTwoFactorTokenAsync(
                        identityUser,
                        _userManager.Options.Tokens.AuthenticatorTokenProvider,
                        sanitizedCode))
                {
                    await _auditService.WriteAsync(
                        "AUTH-MFA-REAUTH-FAILURE",
                        userId: identityUser.LegacyUserId,
                        companyId: identityUser.CompanyId,
                        email: identityUser.Email,
                        reason: "InvalidAuthenticatorCode");
                    throw new InvalidOperationException("The authenticator code is invalid.");
                }

                return;
            }

            var recoveryResult = await RedeemRecoveryCodeAsync(identityUser, dto.RecoveryCode);
            if (!recoveryResult.Succeeded)
            {
                await _auditService.WriteAsync(
                    "AUTH-MFA-REAUTH-FAILURE",
                    userId: identityUser.LegacyUserId,
                    companyId: identityUser.CompanyId,
                    email: identityUser.Email,
                    reason: "InvalidRecoveryCode");
                throw new InvalidOperationException("The recovery code is invalid.");
            }
        }

        private async Task<MfaSetupDTO> BuildSetupDtoAsync(ApplicationUser identityUser)
        {
            var authenticatorKey = await EnsureAuthenticatorKeyAsync(identityUser);
            return new MfaSetupDTO
            {
                IsTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(identityUser),
                SharedKey = authenticatorKey,
                AuthenticatorUri = BuildAuthenticatorUri(identityUser.Email ?? identityUser.UserName ?? string.Empty, authenticatorKey)
            };
        }

        private async Task<string> EnsureAuthenticatorKeyAsync(ApplicationUser identityUser)
        {
            var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(identityUser);
            if (!string.IsNullOrWhiteSpace(authenticatorKey))
            {
                return authenticatorKey;
            }

            identityUser.UpdatedAt = DateTime.UtcNow;
            EnsureIdentitySucceeded(await _userManager.ResetAuthenticatorKeyAsync(identityUser), "InitializeAuthenticatorKey");
            var refreshedUser = await RequireIdentityUserByIdAsync(identityUser.Id);
            authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(refreshedUser);
            if (string.IsNullOrWhiteSpace(authenticatorKey))
            {
                throw new InvalidOperationException("Unable to initialize the authenticator key for this account.");
            }

            return authenticatorKey;
        }

        private string BuildAuthenticatorUri(string email, string sharedKey)
        {
            var label = Uri.EscapeDataString($"{_issuer}:{email}");
            var encodedIssuer = Uri.EscapeDataString(_issuer);
            return $"otpauth://totp/{label}?secret={sharedKey}&issuer={encodedIssuer}";
        }

        private async Task<ApplicationUser> RequireIdentityUserAsync(int legacyUserId)
        {
            return await _identityAccountService.FindByLegacyUserIdAsync(legacyUserId)
                ?? throw new InvalidOperationException("Identity user was not found for this account.");
        }

        private async Task<ApplicationUser> RequireIdentityUserByIdAsync(int identityUserId)
        {
            return await _userManager.Users.FirstOrDefaultAsync(u => u.Id == identityUserId)
                ?? throw new InvalidOperationException("Identity user was not found for this account.");
        }

        private static int CountProvidedMethods(MfaReauthenticationDTO dto)
        {
            var count = 0;
            if (!string.IsNullOrWhiteSpace(dto.CurrentPassword))
            {
                count++;
            }

            if (!string.IsNullOrWhiteSpace(dto.TwoFactorCode))
            {
                count++;
            }

            if (!string.IsNullOrWhiteSpace(dto.RecoveryCode))
            {
                count++;
            }

            return count;
        }

        private static string NormalizeAuthenticatorCode(string code)
        {
            return new string(code.Where(char.IsDigit).ToArray());
        }

        private static string NormalizeRecoveryCode(string code)
        {
            return new string(code
                .Where(character => !char.IsWhiteSpace(character) && character != '-')
                .ToArray());
        }

        private async Task<IdentityResult> RedeemRecoveryCodeAsync(ApplicationUser identityUser, string recoveryCode)
        {
            var trimmedRecoveryCode = recoveryCode.Trim();
            if (string.IsNullOrWhiteSpace(trimmedRecoveryCode))
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "InvalidRecoveryCode",
                    Description = "The recovery code is invalid."
                });
            }

            var normalizedRecoveryCode = NormalizeRecoveryCode(trimmedRecoveryCode);
            var candidates = new[] { trimmedRecoveryCode, normalizedRecoveryCode }
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Distinct(StringComparer.Ordinal);

            IdentityResult? lastResult = null;
            foreach (var candidate in candidates)
            {
                lastResult = await _userManager.RedeemTwoFactorRecoveryCodeAsync(identityUser, candidate);
                if (lastResult.Succeeded)
                {
                    return lastResult;
                }
            }

            return lastResult ?? IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidRecoveryCode",
                Description = "The recovery code is invalid."
            });
        }

        private static bool IsValidAuthenticatorCode(string code)
        {
            return code.Length == 6 && code.All(char.IsDigit);
        }

        private static void EnsureIdentitySucceeded(IdentityResult result, string operation)
        {
            if (result.Succeeded)
            {
                return;
            }

            var details = string.Join("; ", result.Errors.Select(e => $"{e.Code}:{e.Description}"));
            throw new InvalidOperationException($"Identity operation '{operation}' failed: {details}");
        }
    }
}
