# Phase 3 Hardening Notes

## What Changed
- Added a shared strong-password policy for `register-company`, admin-created users, and password changes.
- Added persistent account lockout tracking on `User` with a configurable 5-attempt / 15-minute default.
- Added endpoint-specific ASP.NET Core rate limiting for:
  - `POST /api/auth/login`
  - `POST /api/auth/register-company`
  - `PUT /api/auth/password`
- Expanded auth/security auditing with sanitized events for login success/failure, lockouts, password changes, profile updates, registration security failures, and auth rate-limit rejections.
- Tightened JWT validation so lifetime validation, required expiration, configurable clock skew, and environment-sensitive `RequireHttpsMetadata` are explicit in both JWT bearer auth and the custom JWT middleware.

## Why
- The password policy raises the floor for new credentials without blocking passphrase-style passwords.
- Lockout and rate limiting reduce brute-force exposure while keeping the existing custom auth flow intact.
- Sanitized auth auditing preserves security visibility without storing raw passwords, tokens, or CAPTCHA data.
- The JWT cleanup closes production-safety gaps without replacing the current token model.

## Configuration Defaults
- `JwtSettings:ClockSkewSeconds = 60`
- `AuthSecurity:Lockout:MaxFailedAccessAttempts = 5`
- `AuthSecurity:Lockout:LockoutMinutes = 15`
- `AuthSecurity:RateLimiting:Login = 5 requests / 60 seconds`
- `AuthSecurity:RateLimiting:RegisterCompany = 3 requests / 600 seconds`
- `AuthSecurity:RateLimiting:ChangePassword = 5 requests / 600 seconds`

## Identity Migration Readiness
- The existing routes, DTOs, JWT claim set, and client token flow remain unchanged.
- The new lockout fields and security auditing are compatible with a later Identity migration because they isolate hardening concerns instead of introducing a second auth system.
- Password hashing remains the current HMACSHA512 implementation in this phase, as requested.

## Manual Test Checklist
- Confirm a valid existing account can still log in and receives the same token shape and claims.
- Attempt 5 failed logins against the same real account and confirm the 5th failure applies a temporary lockout.
- Wait for the configured lockout period or update `LockoutEndUtc` in local data, then confirm login succeeds again with the correct password.
- Confirm weak passwords are rejected in:
  - company registration
  - admin user creation
  - change password
- Burst `POST /api/auth/login`, `POST /api/auth/register-company`, and `PUT /api/auth/password` until a `429` response is returned with a `Retry-After` header.
- Review `AuditLogs` entries and confirm auth/security events are present without raw passwords, JWTs, Authorization headers, or CAPTCHA tokens.
- Confirm `POST /api/users` audit entries no longer store the submitted password payload.
