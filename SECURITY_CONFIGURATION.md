# SECURITY_CONFIGURATION

## Purpose

This document defines how to provide secrets and sensitive runtime configuration for `AccountingSystem.Api` without committing them to source control.

The API project already includes `UserSecretsId` `ce28e0cf-6ba6-410e-8e21-b30433dfe4dc`, so local development should use ASP.NET Core user-secrets or environment variables. Production deployments should use environment variables or a managed secret store.

## Required Configuration Keys

### Always required at startup

- `ConnectionStrings__DefaultConnection`
- `JwtSettings__Secret`
- `JwtSettings__Issuer`
- `JwtSettings__Audience`
- `JwtSettings__ExpiryMinutes`
- `JwtSettings__ClockSkewSeconds`
- `AuthSecurity__Lockout__MaxFailedAccessAttempts`
- `AuthSecurity__Lockout__LockoutMinutes`
- `AuthSecurity__RateLimiting__Login__PermitLimit`
- `AuthSecurity__RateLimiting__Login__WindowSeconds`
- `AuthSecurity__RateLimiting__RegisterCompany__PermitLimit`
- `AuthSecurity__RateLimiting__RegisterCompany__WindowSeconds`
- `AuthSecurity__RateLimiting__ChangePassword__PermitLimit`
- `AuthSecurity__RateLimiting__ChangePassword__WindowSeconds`
- `AuthSecurity__RateLimiting__ForgotPassword__PermitLimit`
- `AuthSecurity__RateLimiting__ForgotPassword__WindowSeconds`
- `AuthSecurity__RateLimiting__ResetPassword__PermitLimit`
- `AuthSecurity__RateLimiting__ResetPassword__WindowSeconds`
- `Smtp__Host`
- `Smtp__Port`
- `Smtp__Username`
- `Smtp__Password`
- `Smtp__FromAddress`
- `Smtp__FromName`
- `Smtp__EnableSsl`
- `AppUrls__ClientBaseUrl`

### Required at startup outside Development

- `PayMongo__SecretKey`
- `Recaptcha__SecretKey`

### Managed configuration, but conditionally required

- `PayMongo__PublicKey`
- `Recaptcha__ScoreThreshold`
- `BootstrapAdmin__Email`
- `BootstrapAdmin__FullName`
- `BootstrapAdmin__InitialPassword`

`PayMongo__PublicKey` remains documented because it is part of the expected configuration shape, even though the current API runtime does not consume it directly.

`BootstrapAdmin__*` values are required only when the database has not yet been initialized with a super-admin account. After the first super-admin exists, those values can be removed or rotated out of the active secret store.

If the database is missing and no super-admin exists yet, set `BootstrapAdmin:Email`, `BootstrapAdmin:FullName`, and `BootstrapAdmin:InitialPassword` before the first API run so startup seeding can complete successfully.

## Local Development Setup

### Recommended approach: user-secrets

Run these commands from `AccountingSystem.Api`:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=YOUR_SERVER;Initial Catalog=AccountingSystemDB;Integrated Security=True;Trust Server Certificate=True"
dotnet user-secrets set "JwtSettings:Secret" "replace-with-a-long-random-secret"
dotnet user-secrets set "JwtSettings:Issuer" "AccountingAPI"
dotnet user-secrets set "JwtSettings:Audience" "AccountingClient"
dotnet user-secrets set "JwtSettings:ExpiryMinutes" "60"
dotnet user-secrets set "JwtSettings:ClockSkewSeconds" "60"
dotnet user-secrets set "AuthSecurity:Lockout:MaxFailedAccessAttempts" "5"
dotnet user-secrets set "AuthSecurity:Lockout:LockoutMinutes" "15"
dotnet user-secrets set "AuthSecurity:RateLimiting:Login:PermitLimit" "5"
dotnet user-secrets set "AuthSecurity:RateLimiting:Login:WindowSeconds" "60"
dotnet user-secrets set "AuthSecurity:RateLimiting:RegisterCompany:PermitLimit" "3"
dotnet user-secrets set "AuthSecurity:RateLimiting:RegisterCompany:WindowSeconds" "600"
dotnet user-secrets set "AuthSecurity:RateLimiting:ChangePassword:PermitLimit" "5"
dotnet user-secrets set "AuthSecurity:RateLimiting:ChangePassword:WindowSeconds" "600"
dotnet user-secrets set "AuthSecurity:RateLimiting:ForgotPassword:PermitLimit" "3"
dotnet user-secrets set "AuthSecurity:RateLimiting:ForgotPassword:WindowSeconds" "900"
dotnet user-secrets set "AuthSecurity:RateLimiting:ResetPassword:PermitLimit" "5"
dotnet user-secrets set "AuthSecurity:RateLimiting:ResetPassword:WindowSeconds" "900"
dotnet user-secrets set "Smtp:Host" "smtp.example.com"
dotnet user-secrets set "Smtp:Port" "587"
dotnet user-secrets set "Smtp:Username" "smtp-user"
dotnet user-secrets set "Smtp:Password" "smtp-password"
dotnet user-secrets set "Smtp:FromAddress" "no-reply@example.com"
dotnet user-secrets set "Smtp:FromName" "AccSys"
dotnet user-secrets set "Smtp:EnableSsl" "true"
dotnet user-secrets set "AppUrls:ClientBaseUrl" "https://localhost:7150"
dotnet user-secrets set "BootstrapAdmin:Email" "superadmin@example.com"
dotnet user-secrets set "BootstrapAdmin:FullName" "Bootstrap Super Admin"
dotnet user-secrets set "BootstrapAdmin:InitialPassword" "Correct horse battery staple 42"
dotnet user-secrets set "PayMongo:SecretKey" "replace-with-paymongo-secret-key"
dotnet user-secrets set "PayMongo:PublicKey" "replace-with-paymongo-public-key"
dotnet user-secrets set "Recaptcha:SecretKey" "replace-with-recaptcha-secret-key"
dotnet user-secrets set "Recaptcha:ScoreThreshold" "0.5"
```

Useful commands:

```powershell
dotnet user-secrets list
dotnet user-secrets remove "PayMongo:SecretKey"
```

### Development strictness

- The API fails fast at startup in Development if these settings are missing:
  - `ConnectionStrings:DefaultConnection`
  - JWT settings
  - auth-security numeric settings
  - SMTP settings
  - `AppUrls:ClientBaseUrl`
- `PayMongo:SecretKey` and `Recaptcha:SecretKey` are not required for the API to boot in Development.
- If those integration secrets are missing, the affected feature fails with a clear configuration error only when used.
- `BootstrapAdmin:*` values are required only when the database has not been initialized with a super-admin account yet.

### Environment variable alternative

PowerShell example:

```powershell
$env:ConnectionStrings__DefaultConnection = "Data Source=YOUR_SERVER;Initial Catalog=AccountingSystemDB;Integrated Security=True;Trust Server Certificate=True"
$env:JwtSettings__Secret = "replace-with-a-long-random-secret"
$env:JwtSettings__Issuer = "AccountingAPI"
$env:JwtSettings__Audience = "AccountingClient"
$env:JwtSettings__ExpiryMinutes = "60"
$env:JwtSettings__ClockSkewSeconds = "60"
$env:AuthSecurity__Lockout__MaxFailedAccessAttempts = "5"
$env:AuthSecurity__Lockout__LockoutMinutes = "15"
$env:AuthSecurity__RateLimiting__Login__PermitLimit = "5"
$env:AuthSecurity__RateLimiting__Login__WindowSeconds = "60"
$env:AuthSecurity__RateLimiting__RegisterCompany__PermitLimit = "3"
$env:AuthSecurity__RateLimiting__RegisterCompany__WindowSeconds = "600"
$env:AuthSecurity__RateLimiting__ChangePassword__PermitLimit = "5"
$env:AuthSecurity__RateLimiting__ChangePassword__WindowSeconds = "600"
$env:AuthSecurity__RateLimiting__ForgotPassword__PermitLimit = "3"
$env:AuthSecurity__RateLimiting__ForgotPassword__WindowSeconds = "900"
$env:AuthSecurity__RateLimiting__ResetPassword__PermitLimit = "5"
$env:AuthSecurity__RateLimiting__ResetPassword__WindowSeconds = "900"
$env:Smtp__Host = "smtp.example.com"
$env:Smtp__Port = "587"
$env:Smtp__Username = "smtp-user"
$env:Smtp__Password = "smtp-password"
$env:Smtp__FromAddress = "no-reply@example.com"
$env:Smtp__FromName = "AccSys"
$env:Smtp__EnableSsl = "true"
$env:AppUrls__ClientBaseUrl = "https://localhost:7150"
$env:BootstrapAdmin__Email = "superadmin@example.com"
$env:BootstrapAdmin__FullName = "Bootstrap Super Admin"
$env:BootstrapAdmin__InitialPassword = "Correct horse battery staple 42"
$env:PayMongo__SecretKey = "replace-with-paymongo-secret-key"
$env:PayMongo__PublicKey = "replace-with-paymongo-public-key"
$env:Recaptcha__SecretKey = "replace-with-recaptcha-secret-key"
$env:Recaptcha__ScoreThreshold = "0.5"
```

## Production Configuration Expectations

- Do not store production secrets in `appsettings.json`.
- Inject sensitive values through:
  - environment variables
  - container/orchestrator secret injection
  - a managed secret store such as Azure Key Vault or equivalent
- Non-Development environments fail fast at startup when required runtime secrets are missing or still set to the checked-in placeholder value.
- The checked-in `AccountingSystem.Api/appsettings.Template.json` documents the expected shape only. It must not be populated with real secrets and committed back to source control.
- Password-reset email delivery depends on the SMTP settings and `AppUrls__ClientBaseUrl` so reset links point to the correct Blazor client host.
- `Update-Database` only applies migrations. The bootstrap seeder runs when the API starts, after both DbContexts migrate successfully.

## Secret Rotation Notes

- **JWT secret rotation**
  - Rotating `JwtSettings__Secret` invalidates all existing JWTs signed with the previous secret.
  - Plan rotation to coincide with a maintenance window or communicate forced re-login behavior.

- **Database credential rotation**
  - Update `ConnectionStrings__DefaultConnection` in the secret store first, then restart or redeploy the API.
  - Verify migrations and startup connectivity after rotation.

- **PayMongo / reCAPTCHA rotation**
  - Update the injected secret values and restart or redeploy the API so the new configuration is loaded.
  - Validate the specific payment or registration flow after rotation.

- **SMTP rotation**
  - Update `Smtp__Username` / `Smtp__Password` and verify forgot-password delivery immediately after rollout.
  - If the sender identity changes, update `Smtp__FromAddress` / `Smtp__FromName` at the same time.

- **Bootstrap admin handling**
  - Treat `BootstrapAdmin__InitialPassword` as a one-time bootstrap secret.
  - After the first super-admin account is created, rotate or remove the bootstrap password from the active secret store.

## Committed Configuration Policy

- `AccountingSystem.Api/appsettings.json` may contain safe defaults and placeholders only.
- `AccountingSystem.Api/appsettings.Template.json` is the sample shape for operators and developers.
- Never commit:
  - live database credentials
  - JWT secrets
  - PayMongo keys
  - reCAPTCHA secrets
  - SMTP credentials
  - bootstrap admin passwords
