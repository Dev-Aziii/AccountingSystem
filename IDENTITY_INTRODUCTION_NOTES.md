# Identity Introduction Notes

## Parallel Operation Model

Phase 5 introduces ASP.NET Core Identity in parallel with the existing custom JWT authentication system.

- Legacy `Users` / `Roles` remain the production source of truth for login, authorization decisions, and current controllers.
- JWT bearer remains the only active client-facing authentication scheme.
- Identity runs side-by-side in the same database through `IdentityAuthDbContext` and the `AspNet*` tables introduced in Phase 4.
- Existing legacy users are not bulk migrated in this phase. They are hydrated into Identity lazily during successful legacy login or other successful legacy account mutations.

## Explicit Identity Options

Identity is configured explicitly in [Program.cs](C:/SoftDev_repo/AccountingSystem/AccountingSystem.Api/Program.cs).

- Password:
  - `RequiredLength = 12`
  - `RequireNonAlphanumeric = false`
  - `RequireLowercase = false`
  - `RequireUppercase = false`
  - `RequireDigit = false`
  - `RequiredUniqueChars = 1`
- Shared password validation:
  - `SharedPasswordIdentityValidator` wraps the existing shared `PasswordPolicy`
  - effective accepted policy remains:
    - 12 to 128 characters with at least 3 of 4 character classes, or
    - 16 to 128 characters with at least 3 words for passphrase-style passwords
- Lockout:
  - `MaxFailedAccessAttempts` comes from `AuthSecurity:Lockout:MaxFailedAccessAttempts`
  - `DefaultLockoutTimeSpan` comes from `AuthSecurity:Lockout:LockoutMinutes`
  - `AllowedForNewUsers = true`
- User:
  - `RequireUniqueEmail = true`
  - allowed username characters are explicitly set to `abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+`
- Tokens:
  - `DataProtectionTokenProviderOptions.TokenLifespan = 2 hours`

## Login Hydration Behavior

Legacy login still authenticates against the current custom user store and legacy HMAC password hashes.

After a successful legacy login and before the JWT response is returned:

1. A `LegacyIdentityUserSnapshot` is created from the legacy user record.
2. `ILegacyIdentityBridgeService.SyncAfterSuccessfulLoginAsync` runs in the background of the successful legacy path.
3. The bridge ensures an `ApplicationUser` exists for that legacy user.
4. Core fields are synchronized:
   - `LegacyUserId`
   - `CompanyId`
   - `Email`
   - `UserName`
   - `FullName`
   - `Status`
   - `IsActive`
   - `IsDeleted`
5. Identity role membership is synchronized to match the legacy role name exactly.
6. If the linked Identity password hash does not match the submitted plaintext password, the Identity password hash and security stamp are refreshed.

This allows Identity to become operational in parallel without switching the live login endpoint yet.

## Legacy-First Failure Semantics

Legacy auth and legacy account updates remain authoritative in this phase.

- If Identity sync succeeds, the linked Identity user stays current.
- If Identity sync fails after a successful legacy operation:
  - the existing API response is not failed
  - the failure is logged through the application logger
  - a sanitized `IDENTITY-SYNC-FAILURE` audit event is written
  - the next successful login or later account mutation can heal the Identity record

This keeps the transition safe and avoids a big-bang auth cutover.

## New Services And Interfaces

- `IIdentityAccountService`
  - Identity-only account management boundary around `UserManager<ApplicationUser>` and `RoleManager<ApplicationRole>`.
- `IdentityAccountService`
  - Provisions linked Identity users, synchronizes core fields, synchronizes roles, and updates Identity password hashes.
- `ILegacyIdentityBridgeService`
  - Legacy-to-Identity translation boundary used by existing auth and account flows.
- `LegacyIdentityBridgeService`
  - Calls the Identity account service from legacy events and absorbs sync failures so the legacy success path remains intact.
- `SharedPasswordIdentityValidator`
  - Reuses the shared `PasswordPolicy` so Identity password acceptance matches the existing custom password policy.
- `LegacyIdentityUserSnapshot`
  - Small internal snapshot model that carries the legacy user data required for synchronization without leaking live EF-tracked entities across boundaries.

## Current Sync Triggers

The following existing legacy flows now synchronize to Identity in parallel:

- successful legacy login
- company registration
- user creation through the existing register path
- profile update
- password change
- user archive / restore
- superadmin user status update / toggle

## Why Current Endpoints And Client Flows Remain Unchanged

- No route or DTO changes were introduced.
- JWT claim shape remains unchanged.
- The Blazor client still uses the same login endpoint and token handling flow.
- Existing role strings remain `Admin`, `Accounting`, `Management`, and `SuperAdmin`.
- Cookie auth, SignInManager-based flows, and new Identity endpoints are intentionally deferred to a later phase.

This phase only makes Identity operational in parallel so later controller and endpoint migration can be done incrementally.
