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

### Required at startup outside Development

- `PayMongo__SecretKey`
- `Recaptcha__SecretKey`

### Managed configuration, but not startup-critical today

- `PayMongo__PublicKey`
- `Recaptcha__ScoreThreshold`

`PayMongo__PublicKey` remains documented because it is part of the expected configuration shape, even though the current API runtime does not consume it directly.

## Local Development Setup

### Recommended approach: user-secrets

Run these commands from `AccountingSystem.Api`:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=YOUR_SERVER;Initial Catalog=AccountingSystemDB;Integrated Security=True;Trust Server Certificate=True"
dotnet user-secrets set "JwtSettings:Secret" "replace-with-a-long-random-secret"
dotnet user-secrets set "JwtSettings:Issuer" "AccountingAPI"
dotnet user-secrets set "JwtSettings:Audience" "AccountingClient"
dotnet user-secrets set "JwtSettings:ExpiryMinutes" "60"
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

- The API fails fast at startup in Development if these core settings are missing:
  - `ConnectionStrings:DefaultConnection`
  - `JwtSettings:Secret`
  - `JwtSettings:Issuer`
  - `JwtSettings:Audience`
  - `JwtSettings:ExpiryMinutes`
- `PayMongo:SecretKey` and `Recaptcha:SecretKey` are not required for the API to boot in Development.
- If those integration secrets are missing, the affected feature fails with a clear configuration error only when used.

### Environment variable alternative

PowerShell example:

```powershell
$env:ConnectionStrings__DefaultConnection = "Data Source=YOUR_SERVER;Initial Catalog=AccountingSystemDB;Integrated Security=True;Trust Server Certificate=True"
$env:JwtSettings__Secret = "replace-with-a-long-random-secret"
$env:JwtSettings__Issuer = "AccountingAPI"
$env:JwtSettings__Audience = "AccountingClient"
$env:JwtSettings__ExpiryMinutes = "60"
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

## Committed Configuration Policy

- `AccountingSystem.Api/appsettings.json` may contain safe defaults and placeholders only.
- `AccountingSystem.Api/appsettings.Template.json` is the sample shape for operators and developers.
- Never commit:
  - live database credentials
  - JWT secrets
  - PayMongo keys
  - reCAPTCHA secrets
